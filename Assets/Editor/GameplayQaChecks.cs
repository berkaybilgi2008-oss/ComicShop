#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode.Transports.UTP;
using Object = UnityEngine.Object;

// Deterministic checks use an unsaved temporary additive scene; never start Relay.
public static class GameplayQaChecks
{
    const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    static int checks;
    static Vector3 origin = new Vector3(10000,10000,10000);
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("[GAMEPLAY QA FAIL] " + label);
        checks++;
    }
    static object Invoke(object obj, string name, params object[] args) =>
        obj.GetType().GetMethod(name,PrivateInstance).Invoke(obj,args);
    static GameObject Cube(string name, Vector3 position)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.position=origin+position;
        return go;
    }
    static BookItem Book(string name, Vector3 position)
    {
        var go=Cube(name,position); go.transform.localScale=Vector3.one*0.1f;
        go.AddComponent<Rigidbody>().isKinematic=true;
        var book=go.AddComponent<BookItem>(); Invoke(book,"Awake"); return book;
    }
    [MenuItem("ComicShop/Tests/Run Gameplay QA Checks (Edit Mode)")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Run outside Play Mode."); return; }
        var oldScene=SceneManager.GetActiveScene();
        var saved=new Dictionary<FieldInfo,object>();
        foreach(var field in typeof(GameStats).GetFields(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))
            if(!field.IsInitOnly && !field.IsLiteral) saved.Add(field,field.GetValue(null));
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene); checks=0;
        Mesh mesh=null;
        try
        {
            GameStats.Initialize(new[] {45,359},2);
            GameStats.RegisterPlacement(359); GameStats.RegisterPlacement(359); GameStats.RegisterPlacement(359);
            Check(GameStats.TotalBooks==4 && GameStats.TotalPlaced==2 && GameStats.CompletedBookGroupCount==1,"sparse BookID and overcount cap");
            GameStats.UnregisterPlacement(359);
            Check(GameStats.TotalPlaced==1 && GameStats.CompletedBookGroupCount==0,"completion rollback");
            bool rejected=false;
            try { GameStats.Initialize(new[] {45,45},2); } catch(ArgumentException) { rejected=true; }
            Check(rejected && GameStats.TotalPlaced==1,"duplicate catalogue rejected atomically");

            GameStats.Initialize(1,10);
            var slotGo=Cube("QA Shelf",new Vector3(0,0,10));
            var slot=slotGo.AddComponent<ShelfSlot>(); slot.capacity=10; slot.brandID=0;
            var book=Book("QA Book",new Vector3(0,0,8));
            Check(slot.PlaceBook(book),"initial shelf placement");
            Check(!slot.PlaceBook(book) && slot.FilledCount==1 && GameStats.TotalPlaced==1,"same book cannot count twice");
            slot.RemoveBook(book);
            Check(slot.FilledCount==0 && !slot.IsClaimed && GameStats.TotalPlaced==0,"shelf claim released");

            var actor=Cube("QA Actor",Vector3.zero);
            var target=Book("QA Reach",new Vector3(0,0,3));
            Physics.SyncTransforms();
            Check(GameplayPhysics.CanReach(actor.transform,origin,target.GetComponent<Collider>(),4),"unobstructed reach");
            var wall=Cube("QA Wall",new Vector3(0,0,1.5f)); wall.layer=2;
            Physics.SyncTransforms();
            Check(!GameplayPhysics.CanReach(actor.transform,origin,target.GetComponent<Collider>(),4),"wall on Ignore Raycast layer still blocks interaction");
            wall.SetActive(false); Physics.SyncTransforms();
            Check(GameplayPhysics.CanReach(actor.transform,origin,target.GetComponent<Collider>(),4),"opening restores reach");

            var floor=new GameObject("QA Concave Surface"); floor.transform.position=origin+Vector3.right*10;
            mesh=new Mesh(); mesh.vertices=new[] {new Vector3(-1,0,-1),new Vector3(-1,0,1),new Vector3(1,0,1),new Vector3(1,0,-1)};
            mesh.triangles=new[] {0,1,2,0,2,3}; mesh.RecalculateBounds();
            var surface=floor.AddComponent<MeshCollider>(); surface.sharedMesh=mesh; surface.convex=false;
            Physics.SyncTransforms();
            Check(GameplayPhysics.SurfaceStillTouches(surface,floor.transform.position,Vector3.up,0.025f),"concave surface contact without unsupported ClosestPoint");
            Check(!GameplayPhysics.SurfaceStillTouches(surface,floor.transform.position+Vector3.right*2,Vector3.up,0.025f),"missing surface cannot support a frozen book");

            var interaction=actor.AddComponent<PlayerInteraction>();
            var held=Book("QA Other Held",new Vector3(1,0,0)); held.transform.SetParent(actor.transform,true);
            var released=Book("QA Released",new Vector3(2,0,0));
            var actorCollider=actor.GetComponent<Collider>(); var releasedCollider=released.GetComponent<Collider>();
            Invoke(interaction,"IgnorePlayerCollision",released,true);
            Check(Physics.GetIgnoreCollision(actorCollider,releasedCollider),"player collision ignored during release");
            Check(!Physics.GetIgnoreCollision(held.GetComponent<Collider>(),releasedCollider),"held child must not create book-to-book ignore pair");
            var list=(IList)typeof(PlayerInteraction).GetField("pendingCollisionRestores",PrivateInstance).GetValue(interaction);
            var type=typeof(PlayerInteraction).GetNestedType("CollisionRestore",BindingFlags.NonPublic);
            var pending=Activator.CreateInstance(type);
            type.GetField("book").SetValue(pending,released);
            type.GetField("deadline").SetValue(pending,Time.unscaledTime-1);
            type.GetField("readyAt").SetValue(pending,Mathf.Max(0,Time.unscaledTime));
            list.Add(pending);
            Invoke(interaction,"CancelHandAnimations"); Invoke(interaction,"UpdateCollisionRestoration");
            Check(!Physics.GetIgnoreCollision(actorCollider,releasedCollider),"hand cancellation must not cancel collision restoration");
            Invoke(interaction,"IgnorePlayerCollision",released,true); Invoke(interaction,"OnDisable");
            Check(!Physics.GetIgnoreCollision(actorCollider,releasedCollider),"disable restores ignored pairs");

            var relay=new UnityRelaySessionTransport();
            var transport=new GameObject("QA Transport").AddComponent<UnityTransport>();
            Invoke(relay,"CheckCurrent",0,transport); relay.Reset(); rejected=false;
            try { Invoke(relay,"CheckCurrent",0,transport); }
            catch(TargetInvocationException error) when(error.InnerException is OperationCanceledException) { rejected=true; }
            Check(rejected,"cancelled Relay generation cannot commit transport settings");
            Debug.Log($"[GAMEPLAY QA PASS] {checks} assertions. Edit-mode checks only; no multiplayer latency/SDK/bake/FPS certification.");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene,true);
            if(oldScene.IsValid() && oldScene.isLoaded) SceneManager.SetActiveScene(oldScene);
            if(mesh!=null) Object.DestroyImmediate(mesh);
            foreach(var entry in saved) entry.Key.SetValue(null,entry.Value);
        }
    }
}
#endif
