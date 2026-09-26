using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Owner-camera view uses forearms/hands only: removing just the head exposes the
// torso's open neck. Full character meshes remain visible to other cameras/players.
// Mesh and renderer changes are restored after rendering; no bone scale changes.
[DefaultExecutionOrder(210)]
[DisallowMultipleComponent]
public sealed class FirstPersonHead : MonoBehaviour
{
    ToastBookCarry rig;
    PlayerInteraction inventory;
    NetworkPlayerSetup network;
    Transform head;
    bool built, applied;
    Camera renderingCamera;

    sealed class Swap { public SkinnedMeshRenderer renderer; public Mesh original, headless; }
    readonly List<Swap> swaps = new List<Swap>();
    readonly List<Renderer> attached = new List<Renderer>();
    readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
    readonly List<Mesh> owned = new List<Mesh>();

    void Awake()
    {
        rig = GetComponent<ToastBookCarry>();
        foreach (Transform bone in GetComponentsInChildren<Transform>(true))
            if (bone.name == "Head" || bone.name.EndsWith(":Head")) { head = bone; break; }
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeginSRP;
        RenderPipelineManager.endCameraRendering += EndSRP;
        Camera.onPreCull += BeginBuiltin;
        Camera.onPostRender += EndBuiltin;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginSRP;
        RenderPipelineManager.endCameraRendering -= EndSRP;
        Camera.onPreCull -= BeginBuiltin;
        Camera.onPostRender -= EndBuiltin;
        Restore();
    }

    void OnDestroy()
    {
        foreach (var mesh in owned) if (mesh) Destroy(mesh);
        owned.Clear();
    }

    void BeginSRP(ScriptableRenderContext context, Camera camera) => Begin(camera);
    void EndSRP(ScriptableRenderContext context, Camera camera) => End(camera);
    void BeginBuiltin(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null) Begin(camera); }
    void EndBuiltin(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null) End(camera); }

    bool IsLocal()
    {
        if (!inventory) inventory = GetComponentInParent<PlayerInteraction>();
        if (!network) network = GetComponentInParent<NetworkPlayerSetup>();
        return head && inventory && inventory.isActiveAndEnabled && inventory.playerCamera &&
            inventory.playerCamera.isActiveAndEnabled && (!network || !network.IsSpawned || network.IsOwner);
    }

    // Ayni kamera arkadan/uzaktan bir gorunum icin kullanilirsa kafa gizlenmez.
    bool IsFirstPersonCamera(Camera camera)
    {
        if (!IsLocal() || camera != inventory.playerCamera) return false;
        Vector3 delta = camera.transform.position - head.position;
        return delta.magnitude < 0.9f;
    }

    void LateUpdate()
    {
        // Baska bir bilesen (atis gorunumu) mesh'i geri koyarken bizim kafasiz mesh'imizi
        // birakmis olabilir; kare basinda her zaman asil mesh'e don.
        Restore();
        if (!IsLocal()) return;
        if (!built) Build();
    }

    internal static bool IsViewArmBone(Transform bone, ToastBookCarry carry)
    {
        if (!bone || !carry) return false;
        return (carry.forearm && (bone == carry.forearm || bone.IsChildOf(carry.forearm))) ||
            (carry.leftForearm && (bone == carry.leftForearm || bone.IsChildOf(carry.leftForearm)));
    }

    void Build()
    {
        built = true;
        var skins = (rig ? (Component)rig : this).GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var skin in skins)
        {
            // Kafa kemiginin altindaki ayri parcalar (sapka, gozluk vb.) dogrudan gizlenir.
            if (skin.transform.IsChildOf(head)) { attached.Add(skin); continue; }
            Mesh original = skin.sharedMesh;
            if (!original) continue;
            if (!original.isReadable)
            {
                // Camera-scoped fallback; never shrink bones seen by a rear camera.
                attached.Add(skin);
                Debug.LogWarning("FirstPersonHead: enable Read/Write on the character mesh to show first-person arms.", this);
                continue;
            }
            var weights = original.boneWeights;
            var bones = skin.bones;
            if (weights == null || weights.Length != original.vertexCount || bones == null) continue;
            var hide = new bool[original.vertexCount];
            bool any = false;
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                int index = w.boneIndex0; float largest = w.weight0;
                if (w.weight1 > largest) { index = w.boneIndex1; largest = w.weight1; }
                if (w.weight2 > largest) { index = w.boneIndex2; largest = w.weight2; }
                if (w.weight3 > largest) index = w.boneIndex3;
                if (index < 0 || index >= bones.Length || !bones[index]) continue;
                if (!IsViewArmBone(bones[index], rig)) { hide[i] = true; any = true; }
            }
            if (!any) continue;
            Mesh headless = Instantiate(original);
            headless.name = original.name + "_FirstPerson";
            for (int sub = 0; sub < original.subMeshCount; sub++)
            {
                int[] triangles = original.GetTriangles(sub);
                var keep = new List<int>(triangles.Length);
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    if (hide[a] || hide[b] || hide[c]) continue;
                    keep.Add(a); keep.Add(b); keep.Add(c);
                }
                headless.SetTriangles(keep, sub);
            }
            owned.Add(headless);
            swaps.Add(new Swap { renderer = skin, original = original, headless = headless });
        }
        foreach (var renderer in head.GetComponentsInChildren<Renderer>(true))
            if (!(renderer is SkinnedMeshRenderer) && !attached.Contains(renderer)) attached.Add(renderer);

    }

    void Begin(Camera camera)
    {
        Restore();
        if (!built || !IsFirstPersonCamera(camera)) return;
        renderingCamera = camera;
        applied = true;
        foreach (var swap in swaps)
            if (swap.renderer && swap.renderer.sharedMesh == swap.original) swap.renderer.sharedMesh = swap.headless;
        foreach (var renderer in attached)
            if (renderer)
            {
                hidden[renderer] = renderer.forceRenderingOff;
                renderer.forceRenderingOff = true;
            }
    }

    void End(Camera camera) { if (camera == renderingCamera) Restore(); }

    void Restore()
    {
        foreach (var swap in swaps)
            if (swap.renderer && swap.renderer.sharedMesh == swap.headless) swap.renderer.sharedMesh = swap.original;
        if (applied)
            foreach (var pair in hidden)
                if (pair.Key) pair.Key.forceRenderingOff = pair.Value;
        hidden.Clear();
        applied = false;
        renderingCamera = null;
    }
}
