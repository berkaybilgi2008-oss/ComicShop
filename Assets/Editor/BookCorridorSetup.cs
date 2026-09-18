using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ComicShop.Rendering;

public static class BookCorridorSetup
{
    [MenuItem("Tools/ComicShop/Books/Create Two Corridor Spawn Areas (Undo)")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Stop Play Mode first."); return; }
        var scene=SceneManager.GetActiveScene();
        var spawners=UnityEngine.Object.FindObjectsByType<BookSpawner>(FindObjectsSortMode.None).Where(s=>s.gameObject.scene==scene).ToArray();
        var rooms=UnityEngine.Object.FindObjectsByType<ToonRoom>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(r=>r.gameObject.scene==scene).ToArray();
        if(spawners.Length!=1 || rooms.Length!=1) { Debug.LogError("Expected one BookSpawner and one ToonRoom in the active scene."); return; }
        var spawner=spawners[0]; var room=rooms[0];
        if(spawner.corridorAreas!=null && spawner.corridorAreas.Any(z=>z))
        { Selection.objects=spawner.corridorAreas.Where(z=>z).Select(z=>(UnityEngine.Object)z.gameObject).ToArray(); Debug.Log("Existing corridor areas selected. Adjust their BoxCollider Center/Size with Edit Collider; existing layout preserved."); return; }
        var matrix=Matrix4x4.TRS(room.transform.position,room.transform.rotation,Vector3.one).inverse;
        var shelves=new List<Bounds>();
        foreach(var root in scene.GetRootGameObjects())
        {
            if(!root.activeInHierarchy || !root.name.StartsWith("RAF",StringComparison.OrdinalIgnoreCase)) continue;
            bool found=false; Bounds bounds=default;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if(!renderer.enabled) continue;
                var world=renderer.bounds;
                for(int i=0;i<8;i++)
                {
                    var p=matrix.MultiplyPoint3x4(world.center+Vector3.Scale(world.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(!found) { bounds=new Bounds(p,Vector3.zero); found=true; } else bounds.Encapsulate(p);
                }
            }
            if(found) shelves.Add(bounds);
        }
        var b=room.roomBounds;
        var central=shelves.Where(s=>Mathf.Abs(s.center.x-b.center.x)<b.size.x*.22f).ToArray();
        if(central.Length==0) { Debug.LogError("Central RAF shelf row could not be identified. Nothing changed."); return; }
        float centerMin=central.Min(s=>s.min.x),centerMax=central.Max(s=>s.max.x);
        float zMin=Mathf.Max(b.min.z+1f,central.Min(s=>s.min.z));
        float zMax=Mathf.Min(b.max.z-1f,central.Max(s=>s.max.z));
        float left=b.min.x+1f,right=b.max.x-1f;
        foreach(var shelf in shelves)
        {
            if(shelf.max.x<centerMin) left=Mathf.Max(left,shelf.max.x+.25f);
            if(shelf.min.x>centerMax) right=Mathf.Min(right,shelf.min.x-.25f);
        }
        float leftEnd=centerMin-.4f,rightStart=centerMax+.4f;
        if(leftEnd-left<1 || right-rightStart<1 || zMax-zMin<1)
        { Debug.LogError("Detected corridors too narrow. Nothing changed."); return; }
        Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Two corridor spawn areas");
        var go=new GameObject("Book Spawn Corridors"); SceneManager.MoveGameObjectToScene(go,scene);
        Undo.RegisterCreatedObjectUndo(go,"Create corridor areas");
        go.transform.SetPositionAndRotation(room.transform.position,room.transform.rotation);
        var a=Create(go.transform,"Left Corridor",left,leftEnd,zMin,zMax,b.min.y);
        var c=Create(go.transform,"Right Corridor",rightStart,right,zMin,zMax,b.min.y);
        Undo.RecordObject(spawner,"Assign corridor areas");
        spawner.corridorAreas=new[]{a,c}; spawner.corridorEdgePadding=.35f;
        EditorUtility.SetDirty(spawner); PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
        EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(group);
        Selection.activeGameObject=spawner.gameObject; SceneView.RepaintAll();
        Debug.Log("[BOOK CORRIDORS] Two areas assigned from current RAF geometry. Select BookSpawner to see green bounds; inspect in top view and save with Ctrl+S. Existing books are unchanged; start a new session to respawn. Area boxes are disabled colliders and do not block players.");
    }
    static BoxCollider Create(Transform parent,string name,float minX,float maxX,float minZ,float maxZ,float floor)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,false);
        go.transform.localPosition=new Vector3((minX+maxX)*.5f,floor,(minZ+maxZ)*.5f);
        var box=go.AddComponent<BoxCollider>(); box.size=new Vector3(maxX-minX,.1f,maxZ-minZ);
        box.enabled=false; box.isTrigger=true; return box;
    }
}
