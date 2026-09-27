using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Gercek birinci sahis: sahibinin kamerasi karakterin gozunden bakar ve kendi
// govdesini, kollarini, bacaklarini gorur; yalnizca kafa (ve boyun) gizlenir.
// Kafa kaldirilinca govdede acilan kucuk boyun deligi calisma zamaninda kapatilir,
// asagi bakinca govdenin ici gorunmez. Diger oyuncular/kameralar karakteri eksiksiz
// gorur. Mesh ve renderer degisiklikleri cizimden sonra geri alinir; kemik olcegi degismez.
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

    /// <summary>Birinci sahis kamerasinda gizlenen kemik: kafa, kafanin altindakiler ve boyun.</summary>
    internal static bool IsHiddenHeadBone(Transform bone, Transform head)
    {
        if (!bone) return false;
        if (head && (bone == head || bone.IsChildOf(head))) return true;
        string name = bone.name;
        return name == "Neck" || name.EndsWith(":Neck");
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
                // Okunamayan mesh kesilemez; kafa kameranin onunu kapatmasin diye bu parca
                // yalnizca sahibinin kamerasinda tamamen gizlenir (diger oyuncular gorur).
                attached.Add(skin);
                Debug.LogWarning("FirstPersonHead: karakter mesh'inde Read/Write kapali; govde birinci sahiste gorunmez.", this);
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
                if (IsHiddenHeadBone(bones[index], head)) { hide[i] = true; any = true; }
            }
            if (!any) continue;
            Mesh headless = FirstPersonMeshCut.Cut(original, hide);
            headless.name = original.name + "_FirstPerson";
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

/// <summary>
/// Gizlenen kemiklere ait ucgenleri cikarir ve bu yuzden govdede acilan yeni
/// delikleri (boyun, omuz) ucgen yelpazesiyle kapatir. Kapak, komsu ucgenin
/// yonunu takip eder; ters normal ya da gorunen ic yuzey olusmaz.
/// </summary>
internal static class FirstPersonMeshCut
{
    struct Edge { public int from, to, submesh; }

    public static Mesh Cut(Mesh original, bool[] hide)
    {
        Mesh result = Object.Instantiate(original);
        var vertices = original.vertices;
        // Ayni konumdaki (UV/normal dikisi) kopya kose noktalarini tek noktada birlestir.
        var weld = new int[vertices.Length];
        var lookup = new Dictionary<Vector3Int, int>(vertices.Length);
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i] * 10000f;
            var key = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
            if (!lookup.TryGetValue(key, out int id)) lookup.Add(key, id = i);
            weld[i] = id;
        }
        var before = new Dictionary<long, int>();
        var after = new Dictionary<long, int>();
        var keptEdges = new Dictionary<long, Edge>();
        var kept = new List<int>[original.subMeshCount];
        for (int sub = 0; sub < original.subMeshCount; sub++)
        {
            int[] triangles = original.GetTriangles(sub);
            kept[sub] = new List<int>(triangles.Length);
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                bool removed = hide[a] || hide[b] || hide[c];
                for (int e = 0; e < 3; e++)
                {
                    int from = e == 0 ? a : e == 1 ? b : c;
                    int to = e == 0 ? b : e == 1 ? c : a;
                    long key = Key(weld[from], weld[to]);
                    before.TryGetValue(key, out int n); before[key] = n + 1;
                    if (removed) continue;
                    after.TryGetValue(key, out int m); after[key] = m + 1;
                    keptEdges[key] = new Edge { from = from, to = to, submesh = sub };
                }
                if (!removed) { kept[sub].Add(a); kept[sub].Add(b); kept[sub].Add(c); }
            }
        }
        // Yeni acilan kenarlar: once iki ucgen paylasiyordu, simdi tek ucgen kaldi.
        // Kapak kenari, kalan ucgenin kenarinin tersidir (to -> from).
        var outgoing = new Dictionary<int, Edge>();
        var broken = new HashSet<int>();
        foreach (var pair in after)
        {
            if (pair.Value != 1 || !before.TryGetValue(pair.Key, out int count) || count < 2) continue;
            Edge edge = keptEdges[pair.Key];
            var cap = new Edge { from = edge.to, to = edge.from, submesh = edge.submesh };
            int start = weld[cap.from];
            if (outgoing.ContainsKey(start)) broken.Add(start);
            else outgoing.Add(start, cap);
        }
        var used = new HashSet<int>();
        var loop = new List<int>();
        foreach (var pair in outgoing)
        {
            if (used.Contains(pair.Key)) continue;
            loop.Clear();
            int submesh = pair.Value.submesh;
            int current = pair.Key;
            bool closed = false;
            while (loop.Count <= outgoing.Count)
            {
                if (broken.Contains(current) || !outgoing.TryGetValue(current, out Edge edge) || !used.Add(current)) break;
                loop.Add(edge.from);
                current = weld[edge.to];
                if (current == pair.Key) { closed = true; break; }
            }
            if (!closed || loop.Count < 3) continue;
            for (int i = 1; i + 1 < loop.Count; i++)
            {
                kept[submesh].Add(loop[0]);
                kept[submesh].Add(loop[i]);
                kept[submesh].Add(loop[i + 1]);
            }
        }
        for (int sub = 0; sub < kept.Length; sub++) result.SetTriangles(kept[sub], sub);
        return result;
    }

    static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
}
