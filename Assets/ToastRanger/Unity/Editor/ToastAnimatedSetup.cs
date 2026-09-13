using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.Rendering;
using System;
using System.IO;
using System.Collections.Generic;

public static class ToastAnimatedSetup
{
    [Serializable] public class RigData {
        public int version; public float[] vertices, normals, uv, weights;
        public int[] joints; public MaterialData[] materials; public SubmeshData[] submeshes;
        public BoneData[] bones; public ClipData[] clips;
    }
    [Serializable] public class MaterialData { public string name; public float[] color; }
    [Serializable] public class SubmeshData { public int[] triangles; }
    [Serializable] public class BoneData { public string name; public int parent; public float[] position; }
    [Serializable] public class Track { public int bone; public float[] positions, rotations; }
    [Serializable] public class ClipData { public string name; public float duration; public float[] times; public Track[] tracks; }

    [MenuItem("Tools/Toast Ranger/Create Animated Player Visual")]
    public static void Build()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (Path.GetFileName(path) != "ToastRangerRig.json") {
            EditorUtility.DisplayDialog("Toast Ranger", "Select ToastRangerRig.json in the Project window first.", "OK"); return;
        }
        var cel = Shader.Find("ToastRanger/Cel URP");
        var outlineShader = Shader.Find("ToastRanger/Outline URP");
        if (!cel || !outlineShader) { Debug.LogError("Import the ToastRanger URP shaders first."); return; }
        var data = JsonUtility.FromJson<RigData>(File.ReadAllText(path));
        if (data == null || data.version != 4) throw new InvalidDataException("Expected Toast Ranger V4 rig data.");
        int count = data.vertices.Length / 3;
        if (data.normals.Length != count * 3 || data.weights.Length != count * 4 || data.joints.Length != count * 4)
            throw new InvalidDataException("Invalid mesh attribute sizes.");
        string parent = Path.GetDirectoryName(path).Replace('\\','/');
        string folder = AssetDatabase.GenerateUniqueAssetPath(parent + "/ToastRangerAnimated");
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        var root = new GameObject("ToastRanger_PlayerVisual");
        try {
            var bones = new Transform[data.bones.Length];
            for (int i = 0; i < bones.Length; i++) {
                var b = data.bones[i];
                bones[i] = new GameObject(b.name).transform;
                bones[i].SetParent(b.parent < 0 ? root.transform : bones[b.parent], false);
                bones[i].localPosition = new Vector3(-b.position[0], b.position[1], b.position[2]);
            }
            var mesh = new Mesh { name = "ToastRangerSkinned", indexFormat = IndexFormat.UInt32 };
            var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count];
            var weights = new BoneWeight[count];
            for (int i = 0; i < count; i++) {
                int v = i * 3, w = i * 4;
                // Reflect X and reverse triangle winding: glTF RH -> Unity LH.
                vertices[i] = new Vector3(-data.vertices[v], data.vertices[v+1], data.vertices[v+2]);
                normals[i] = new Vector3(-data.normals[v], data.normals[v+1], data.normals[v+2]);
                uv[i] = new Vector2(data.uv[i*2], data.uv[i*2+1]);
                weights[i] = new BoneWeight { boneIndex0=data.joints[w], boneIndex1=data.joints[w+1], boneIndex2=data.joints[w+2], boneIndex3=data.joints[w+3], weight0=data.weights[w], weight1=data.weights[w+1], weight2=data.weights[w+2], weight3=data.weights[w+3] };
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.boneWeights = weights;
            var bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * root.transform.localToWorldMatrix;
            mesh.bindposes = bindposes; mesh.subMeshCount = data.submeshes.Length;
            var outlineIndices = new List<int>();
            var materials = new Material[data.materials.Length];
            for (int i = 0; i < data.submeshes.Length; i++) {
                int[] tri = (int[])data.submeshes[i].triangles.Clone();
                for (int j = 0; j < tri.Length; j += 3) { int temp=tri[j+1]; tri[j+1]=tri[j+2]; tri[j+2]=temp; }
                mesh.SetTriangles(tri, i);
                // Tiny blush stays flat and receives no expanded ink shell.
                if (data.materials[i].name != "Blush") outlineIndices.AddRange(tri);
                var d = data.materials[i];
                var mat = new Material(cel) { name = d.name };
                mat.SetColor("_BaseColor", new Color(d.color[0], d.color[1], d.color[2], 1));
                AssetDatabase.CreateAsset(mat, folder + "/" + d.name + ".mat"); materials[i]=mat;
            }
            mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, folder + "/Body.asset");
            var body = new GameObject("Body"); body.transform.SetParent(root.transform, false);
            var renderer = body.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh=mesh; renderer.bones=bones; renderer.rootBone=bones[0]; renderer.sharedMaterials=materials;
            renderer.quality=SkinQuality.Bone4;
            renderer.localBounds = new Bounds(new Vector3(0,1.15f,0), new Vector3(1.6f,3.0f,2.0f));
            var shell = UnityEngine.Object.Instantiate(mesh); shell.name="ToastRangerInk";
            shell.subMeshCount=1; shell.SetTriangles(outlineIndices,0); AssetDatabase.CreateAsset(shell,folder+"/Outline.asset");
            var ink = new Material(outlineShader) { name="Outline" }; AssetDatabase.CreateAsset(ink,folder+"/Outline.mat");
            var outline = new GameObject("Outline"); outline.transform.SetParent(root.transform,false);
            var or = outline.AddComponent<SkinnedMeshRenderer>(); or.sharedMesh=shell; or.bones=bones; or.rootBone=bones[0];
            or.sharedMaterial=ink; or.quality=SkinQuality.Bone4; or.localBounds=renderer.localBounds;
            or.shadowCastingMode=ShadowCastingMode.Off; or.receiveShadows=false;

            var animationClips = new Dictionary<string,AnimationClip>();
            foreach (var c in data.clips) {
                var clip = new AnimationClip { name=c.name, frameRate=50, legacy=false, wrapMode=WrapMode.Loop };
                foreach (var track in c.tracks) {
                    string bonePath=AnimationUtility.CalculateTransformPath(bones[track.bone],root.transform);
                    for (int axis=0; axis<3; axis++) SetCurve(clip,bonePath,"m_LocalPosition."+"xyz"[axis],c.times,track.positions,3,axis,axis==0?-1:1);
                    for (int axis=0; axis<4; axis++) SetCurve(clip,bonePath,"m_LocalRotation."+"xyzw"[axis],c.times,track.rotations,4,axis,axis==1||axis==2?-1:1);
                }
                clip.EnsureQuaternionContinuity();
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.loopBlend=false;
                AnimationUtility.SetAnimationClipSettings(clip,settings);
                AssetDatabase.CreateAsset(clip,folder+"/"+c.name+".anim");animationClips.Add(c.name,clip);
            }
            var controller=AnimatorController.CreateAnimatorControllerAtPath(folder+"/Locomotion.controller");
            controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
            controller.AddParameter("Carry",AnimatorControllerParameterType.Float);
            var state=controller.layers[0].stateMachine.AddState("Locomotion");
            controller.layers[0].stateMachine.defaultState=state;
            var tree=new BlendTree { name="Idle Walk Run",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false };
            tree.AddChild(animationClips["Idle"],0);tree.AddChild(animationClips["Walk"],1);tree.AddChild(animationClips["Run"],2);
            AssetDatabase.AddObjectToAsset(tree,controller);
            var bookTree=new BlendTree { name="Book Idle Walk Run",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false };
            bookTree.AddChild(animationClips["IdleBook"],0);bookTree.AddChild(animationClips["WalkBook"],1);bookTree.AddChild(animationClips["RunBook"],2);
            AssetDatabase.AddObjectToAsset(bookTree,controller);
            var carryTree=new BlendTree { name="Carry Blend",blendType=BlendTreeType.Simple1D,blendParameter="Carry",useAutomaticThresholds=false };
            carryTree.AddChild(tree,0);carryTree.AddChild(bookTree,1);
            AssetDatabase.AddObjectToAsset(carryTree,controller);state.motion=carryTree;
            var animator=root.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            var driver=root.AddComponent<ToastLocomotion>();driver.animator=animator;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var carry=root.AddComponent<ToastBookCarry>();carry.animator=animator;
            for(int i=0;i<bones.Length;i++) {
                if(bones[i].name=="RightUpperArm")carry.upperArm=bones[i];
                if(bones[i].name=="RightForearm")carry.forearm=bones[i];
                if(bones[i].name=="RightHand")carry.hand=bones[i];
            }
            var socket=new GameObject("BookSocket").transform;socket.SetParent(carry.hand,false);
            socket.localPosition=new Vector3(0,-.06f,.025f);carry.bookSocket=socket;
            var book=new GameObject("PreviewBook");book.transform.SetParent(socket,false);
            var paper=new Material(cel){name="BookPages"};paper.SetColor("_BaseColor",new Color(.93f,.86f,.70f,1));
            var cover=new Material(cel){name="BookCover"};cover.SetColor("_BaseColor",new Color(.53f,.12f,.08f,1));
            AssetDatabase.CreateAsset(paper,folder+"/BookPages.mat");AssetDatabase.CreateAsset(cover,folder+"/BookCover.mat");
            BookPart(book.transform,"Pages",Vector3.zero,new Vector3(.15f,.22f,.018f),paper);
            BookPart(book.transform,"FrontCover",new Vector3(0,0,.011f),new Vector3(.16f,.23f,.004f),cover);
            BookPart(book.transform,"BackCover",new Vector3(0,0,-.011f),new Vector3(.16f,.23f,.004f),cover);
            carry.previewBook=book;book.SetActive(false);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,folder+"/ToastRanger_PlayerVisual.prefab");
            AssetDatabase.SaveAssets();Selection.activeObject=prefab;EditorGUIUtility.PingObject(prefab);
        } finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static void BookPart(Transform parent,string name,Vector3 position,Vector3 size,Material material)
    {
        var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(parent,false);
        part.transform.localPosition=position;part.transform.localScale=size;
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());part.GetComponent<MeshRenderer>().sharedMaterial=material;
    }
    static void SetCurve(AnimationClip clip,string path,string property,float[] times,float[] values,int stride,int axis,float sign)
    {
        var curve=new AnimationCurve();
        for(int i=0;i<times.Length;i++) curve.AddKey(new Keyframe(times[i],values[i*stride+axis]*sign));
        for(int i=0;i<curve.length;i++) {
            AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);
        }
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
    }
}
