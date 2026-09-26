using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Oyuncu kendi kamerasiyla bakarken kendi kafasini ve sapkasini gormesin:
// asagi bakinca yuz, yukari bakinca sapka kameraya giriyordu.
// Yalnizca sahibinin birinci sahis kamerasi cizilirken kafa ucgenleri gizlenir;
// diger oyuncular ve diger kameralar karakteri eksiksiz gorur. Fizik/ag etkilenmez.
[DefaultExecutionOrder(210)]
[DisallowMultipleComponent]
public sealed class FirstPersonHead : MonoBehaviour
{
    ToastBookCarry rig;
    PlayerInteraction inventory;
    NetworkPlayerSetup network;
    Transform head;
    bool built, applied, scaledFallback;
    Camera renderingCamera;
    Vector3 headScale;

    sealed class Swap { public SkinnedMeshRenderer renderer; public Mesh original, headless; }
    readonly List<Swap> swaps = new List<Swap>();
    readonly List<Renderer> attached = new List<Renderer>();
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
        RestoreScale();
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
        if (!IsLocal()) { RestoreScale(); return; }
        if (!built) Build();
        if (scaledFallback)
        {
            // Okunamayan mesh: kafa kemigini kucult (yalnizca bu oyuncunun kendi bilgisayarinda).
            if (headScale == Vector3.zero) headScale = head.localScale;
            head.localScale = headScale * 0.001f;
        }
    }

    void RestoreScale()
    {
        if (scaledFallback && head && headScale != Vector3.zero) head.localScale = headScale;
    }

    void Build()
    {
        built = true;
        var skins = (rig ? (Component)rig : this).GetComponentsInChildren<SkinnedMeshRenderer>(true);
        bool unreadable = false;
        foreach (var skin in skins)
        {
            // Kafa kemiginin altindaki ayri parcalar (sapka, gozluk vb.) dogrudan gizlenir.
            if (skin.transform.IsChildOf(head)) { attached.Add(skin); continue; }
            Mesh original = skin.sharedMesh;
            if (!original) continue;
            if (!original.isReadable) { unreadable = true; continue; }
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
                if (bones[index] == head || bones[index].IsChildOf(head)) { hide[i] = true; any = true; }
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
        scaledFallback = unreadable && swaps.Count == 0;
        if (scaledFallback)
            Debug.LogWarning("FirstPersonHead: karakter mesh'i okunamiyor (Read/Write kapali); kafa kemigi kucultuluyor.", this);
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
            if (renderer) renderer.forceRenderingOff = true;
    }

    void End(Camera camera) { if (camera == renderingCamera) Restore(); }

    void Restore()
    {
        foreach (var swap in swaps)
            if (swap.renderer && swap.renderer.sharedMesh == swap.headless) swap.renderer.sharedMesh = swap.original;
        if (applied)
            foreach (var renderer in attached)
                if (renderer) renderer.forceRenderingOff = false;
        applied = false;
        renderingCamera = null;
    }
}
