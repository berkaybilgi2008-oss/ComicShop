using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Owner-camera presentation only. No BookItem, Rigidbody, Collider or NetworkObject
// is cloned. World poses remain authoritative for physics and other players.
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class FirstPersonThrowView : MonoBehaviour
{
    ToastBookCarry rig;
    PlayerInteraction inventory;
    NetworkPlayerSetup network;
    GameObject visualRoot, bookRoot;
    Transform a, b, c, head;
    Quaternion wristRest;
    BookItem activeBook;
    bool left, ready, renderingApplied;
    Camera renderingCamera;
    float enteredAt;
    Vector3 entryPosition, entryScale;
    Quaternion entryRotation;
    readonly Dictionary<Transform, Transform> bones = new Dictionary<Transform, Transform>();
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly List<Renderer> visuals = new List<Renderer>();
    readonly List<Renderer> bookVisuals = new List<Renderer>();
    readonly List<SkinPair> skins = new List<SkinPair>();
    readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
    Renderer[] worldBookRenderers;
    sealed class SkinPair
    {
        public SkinnedMeshRenderer source;
        public Mesh bodyOnly, saved;
    }

    void Awake()
    {
        rig = GetComponent<ToastBookCarry>();
        foreach (Transform bone in GetComponentsInChildren<Transform>(true))
            if (bone.name == "Head") { head = bone; break; }
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
        RestoreRendering();
        Clear();
    }
    void BeginSRP(ScriptableRenderContext context, Camera camera) => Begin(camera);
    void EndSRP(ScriptableRenderContext context, Camera camera) => End(camera);
    void BeginBuiltin(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null) Begin(camera); }
    void EndBuiltin(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null) End(camera); }

    bool IsLocal()
    {
        if (!inventory) inventory = GetComponentInParent<PlayerInteraction>();
        if (!network) network = GetComponentInParent<NetworkPlayerSetup>();
        return rig && rig.isActiveAndEnabled && inventory && inventory.isActiveAndEnabled && inventory.playerCamera &&
            inventory.playerCamera.isActiveAndEnabled && (!network || !network.IsSpawned || network.IsOwner);
    }

    bool IsFirstPersonCamera(Camera camera)
    {
        if (!IsLocal() || camera != inventory.playerCamera || !head) return false;
        // A debug/rear view can reuse the SAME Camera component.
        Vector3 delta = camera.transform.position - head.position;
        float horizontal = Vector3.ProjectOnPlane(delta, rig.transform.up).magnitude;
        return horizontal < 0.55f && Mathf.Abs(Vector3.Dot(delta, rig.transform.up)) < 0.65f;
    }

    void LateUpdate()
    {
        RestoreRendering();
        if (!IsLocal() || !IsFirstPersonCamera(inventory.playerCamera) || !inventory.IsThrowPoseActive || !inventory.ActiveHeldBook)
        {
            ready = false;
            activeBook = null;
            return;
        }
        bool useLeft = inventory.throwHand == PlayerInteraction.ThrowHand.Left;
        BookItem book = inventory.ActiveHeldBook;
        if (!visualRoot || left != useLeft)
        {
            Clear();
            left = useLeft;
            if (!BuildArm()) return;
        }
        if (activeBook != book)
        {
            BuildBook(book);
            activeBook = book;
            enteredAt = Time.time;
            entryPosition = book.transform.position;
            entryRotation = book.transform.rotation;
            entryScale = book.transform.lossyScale;
        }
        if (bookVisuals.Count == 0) return;
        foreach (var pair in bones)
        {
            if (!pair.Key || !pair.Value) continue;
            pair.Value.localPosition = pair.Key.localPosition;
            pair.Value.localRotation = pair.Key.localRotation;
            pair.Value.localScale = pair.Key.localScale;
        }
        visualRoot.transform.SetPositionAndRotation(rig.transform.position, rig.transform.rotation);
        visualRoot.transform.localScale = rig.transform.lossyScale;
        PoseView(book);
        ready = true;
    }

    Transform CopyBone(Transform source)
    {
        if (!source || source == rig.transform) return visualRoot.transform;
        if (bones.TryGetValue(source, out var found)) return found;
        var copy = new GameObject(source.name).transform;
        copy.SetParent(CopyBone(source.parent), false);
        copy.localPosition = source.localPosition;
        copy.localRotation = source.localRotation;
        copy.localScale = source.localScale;
        bones.Add(source, copy);
        return copy;
    }

    bool BuildArm()
    {
        Transform upper = left ? rig.leftUpperArm : rig.upperArm;
        Transform lower = left ? rig.leftForearm : rig.forearm;
        Transform hand = left ? rig.leftHand : rig.hand;
        if (!upper || !lower || !hand) return false;
        visualRoot = new GameObject("FirstPersonThrowVisual_Only");
        a = CopyBone(upper); b = CopyBone(lower); c = CopyBone(hand);
        wristRest = rig.GetThrowWristRest(left);
        foreach (var source in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh original = source.sharedMesh;
            if (!original || !original.isReadable) continue;
            var weights = original.boneWeights;
            var sourceBones = source.bones;
            var armVertex = new bool[original.vertexCount];
            var hideVertex = new bool[original.vertexCount];
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                int index = w.boneIndex0; float largest = w.weight0;
                if (w.weight1 > largest) { index = w.boneIndex1; largest = w.weight1; }
                if (w.weight2 > largest) { index = w.boneIndex2; largest = w.weight2; }
                if (w.weight3 > largest) index = w.boneIndex3;
                if (index < 0 || index >= sourceBones.Length || !sourceBones[index]) continue;
                Transform bone = sourceBones[index];
                armVertex[i] = bone == upper || bone.IsChildOf(upper);
                bool head = false;
                for (Transform t = bone; t && t != rig.transform; t = t.parent)
                    if (t.name == "Head" || t.name == "Neck") head = true;
                hideVertex[i] = armVertex[i] || head;
            }
            Mesh armMesh = Instantiate(original), bodyMesh = Instantiate(original);
            meshes.Add(armMesh); meshes.Add(bodyMesh);
            int kept = 0;
            for (int sub = 0; sub < original.subMeshCount; sub++)
            {
                int[] triangles = original.GetTriangles(sub);
                var armTriangles = new List<int>(); var bodyTriangles = new List<int>();
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int x = triangles[t], y = triangles[t + 1], z = triangles[t + 2];
                    if (armVertex[x] && armVertex[y] && armVertex[z])
                    { armTriangles.Add(x); armTriangles.Add(y); armTriangles.Add(z); kept++; }
                    if (!hideVertex[x] && !hideVertex[y] && !hideVertex[z])
                    { bodyTriangles.Add(x); bodyTriangles.Add(y); bodyTriangles.Add(z); }
                }
                armMesh.SetTriangles(armTriangles, sub);
                bodyMesh.SetTriangles(bodyTriangles, sub);
            }
            if (kept == 0) continue;
            var go = new GameObject(source.name + "_ViewArm");
            go.transform.SetParent(CopyBone(source.transform.parent), false);
            go.transform.localPosition = source.transform.localPosition;
            go.transform.localRotation = source.transform.localRotation;
            go.transform.localScale = source.transform.localScale;
            var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = armMesh; renderer.sharedMaterials = source.sharedMaterials;
            var mapped = new Transform[sourceBones.Length];
            for (int i = 0; i < mapped.Length; i++) mapped[i] = CopyBone(sourceBones[i]);
            renderer.bones = mapped; renderer.rootBone = CopyBone(source.rootBone);
            renderer.updateWhenOffscreen = true;
            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 20f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.enabled = false; visuals.Add(renderer);
            skins.Add(new SkinPair { source = source, bodyOnly = bodyMesh });
        }
        if (visuals.Count > 0) return true;
        Debug.LogWarning("First-person throw needs readable skinned arm meshes; retaining world view.", this);
        Clear(); return false;
    }

    void BuildBook(BookItem book)
    {
        if (bookRoot) Destroy(bookRoot);
        bookVisuals.Clear();
        bookRoot = new GameObject("FirstPersonBookVisual_Only");
        worldBookRenderers = book.GetComponentsInChildren<Renderer>(true);
        CopyBookNode(book.transform, bookRoot.transform);
    }
    void CopyBookNode(Transform source, Transform target)
    {
        var filter = source.GetComponent<MeshFilter>();
        var original = source.GetComponent<MeshRenderer>();
        if (filter && filter.sharedMesh && original)
        {
            target.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = target.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = original.sharedMaterials;
            var properties = new MaterialPropertyBlock(); original.GetPropertyBlock(properties);
            renderer.SetPropertyBlock(properties);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.enabled = false; bookVisuals.Add(renderer);
        }
        foreach (Transform child in source)
        {
            if (!child.gameObject.activeSelf) continue;
            var copy = new GameObject(child.name).transform;
            copy.SetParent(target, false);
            copy.localPosition = child.localPosition; copy.localRotation = child.localRotation;
            copy.localScale = child.localScale;
            CopyBookNode(child, copy);
        }
    }

    void PoseView(BookItem book)
    {
        Camera camera = inventory.playerCamera; Transform view = camera.transform;
        float side = left ? -1f : 1f;
        float charge = inventory.ThrowCharge, release = inventory.ThrowReleaseProgress;
        float factor = inventory.chargeScaleMultiplier * 0.65f;
        Quaternion rotation = book.GetAlignedRotation(view.right * side - view.forward * 0.25f,
            Quaternion.AngleAxis(release * 35f, view.right) * view.up);
        book.GetAxisFrame(out Vector3 cover, out Vector3 along, out Vector3 wide, out Vector3 half);
        cover = rotation * cover; along = rotation * along; wide = rotation * wide;
        half *= factor;
        if (Vector3.Dot(wide, view.forward) < 0f) wide = -wide;
        Vector3 center = camera.ViewportToWorldPoint(new Vector3(left ? 0.26f : 0.74f,
            0.54f + charge * 0.04f, Mathf.Max(0.72f, camera.nearClipPlane + 0.4f)));
        // Fit all corners at the actual aspect/FOV, increasing visual depth if needed.
        for (int pass = 0; pass < 5; pass++)
        {
            float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 p = center + cover * (half.x * ((corner & 1) == 0 ? -1f : 1f))
                    + along * (half.y * ((corner & 2) == 0 ? -1f : 1f))
                    + wide * (half.z * ((corner & 4) == 0 ? -1f : 1f));
                Vector3 screen = camera.WorldToViewportPoint(p);
                minX = Mathf.Min(minX, screen.x); maxX = Mathf.Max(maxX, screen.x);
                minY = Mathf.Min(minY, screen.y); maxY = Mathf.Max(maxY, screen.y);
            }
            if (maxX - minX > 0.8f || maxY - minY > 0.8f) { center += view.forward * 0.15f; continue; }
            Vector3 viewport = camera.WorldToViewportPoint(center);
            viewport.x += Mathf.Max(0f, 0.08f - minX) - Mathf.Max(0f, maxX - 0.92f);
            viewport.y += Mathf.Max(0f, 0.08f - minY) - Mathf.Max(0f, maxY - 0.90f);
            center = camera.ViewportToWorldPoint(viewport);
        }
        float enter = Mathf.SmoothStep(0f, 1f, (Time.time - enteredAt) / Mathf.Max(0.01f, inventory.chargeEnterDuration));
        center = Vector3.Lerp(entryPosition, center, enter);
        rotation = Quaternion.Slerp(entryRotation, rotation, enter);
        Vector3 scale = Vector3.Lerp(entryScale, book.OriginalScale * factor, enter);
        // Meet the one authoritative book at release instead of spawning another projectile.
        float handoff = release; // Already cubic in PlayerInteraction; do not ease it twice.
        center = Vector3.Lerp(center, book.transform.position, handoff);
        center += inventory.CurrentThrowShake * (1f - handoff);
        rotation = Quaternion.Slerp(rotation, book.transform.rotation, handoff);
        scale = Vector3.Lerp(scale, book.transform.lossyScale, handoff);
        bookRoot.transform.SetPositionAndRotation(center, rotation); bookRoot.transform.localScale = scale;
        book.GetAxisFrame(out cover, out along, out wide, out half);
        half *= scale.magnitude / Mathf.Max(0.0001f, book.OriginalScale.magnitude);
        cover = rotation * cover; along = rotation * along; wide = rotation * wide;
        if (Vector3.Dot(wide, view.forward) < 0f) wide = -wide;
        Vector3 palm = center - along * (half.y * 0.92f) + wide * (half.z * 0.86f) + cover * half.x;
        Quaternion grip = Quaternion.LookRotation(-cover, -along);
        Vector3 wrist = palm - grip * Vector3.Scale(new Vector3(0f, -0.06f, 0.025f), c.lossyScale);
        float l1 = Vector3.Distance(a.position, b.position), l2 = Vector3.Distance(b.position, c.position);
        Vector3 upper = (view.forward * 0.8f + view.right * side * 0.3f + view.up * 0.5f).normalized;
        Vector3 bend = Vector3.ProjectOnPlane(view.up, upper).normalized;
        float radians = 65f * Mathf.Deg2Rad;
        Vector3 lower = bend * Mathf.Sin(radians) - upper * Mathf.Cos(radians);
        lower = Vector3.Slerp(lower, upper, release * 0.8f);
        Vector3 elbow = wrist - lower * l2;
        // Translate the whole visual skeleton. Moving the shoulder bone alone
        // stretched vertices weighted partly to its chest/parent bones.
        visualRoot.transform.position += elbow - upper * l1 - a.position;
        a.rotation = Quaternion.FromToRotation(b.position - a.position, elbow - a.position) * a.rotation;
        b.rotation = Quaternion.FromToRotation(c.position - b.position, wrist - b.position) * b.rotation;
        // Keep the wrist within the same anatomical limit as the world rig.
        c.rotation = Quaternion.RotateTowards(b.rotation * wristRest, grip, 55f);
        // After limiting wrist rotation, put the book on the actual palm.
        Vector3 actualPalm = c.position + c.rotation *
            Vector3.Scale(new Vector3(0f, -0.06f, 0.025f), c.lossyScale);
        bookRoot.transform.position += actualPalm - palm;
    }

    void Begin(Camera camera)
    {
        RestoreRendering();
        if (!ready || !IsFirstPersonCamera(camera) ||
            !inventory.IsThrowPoseActive || activeBook != inventory.ActiveHeldBook) return;
        renderingCamera = camera;
        renderingApplied = true;
        foreach (var skin in skins)
        {
            if (!skin.source) continue;
            skin.saved = skin.source.sharedMesh; skin.source.sharedMesh = skin.bodyOnly;
        }
        if (worldBookRenderers != null) foreach (var renderer in worldBookRenderers)
        {
            if (!renderer) continue;
            hidden[renderer] = renderer.forceRenderingOff; renderer.forceRenderingOff = true;
        }
        foreach (var renderer in visuals) if (renderer) renderer.enabled = true;
        foreach (var renderer in bookVisuals) if (renderer) renderer.enabled = true;
    }
    void End(Camera camera) { if (camera == renderingCamera) RestoreRendering(); }
    void RestoreRendering()
    {
        if (renderingApplied)
            foreach (var skin in skins) if (skin.source && skin.saved) skin.source.sharedMesh = skin.saved;
        foreach (var pair in hidden) if (pair.Key) pair.Key.forceRenderingOff = pair.Value;
        hidden.Clear(); renderingCamera = null; renderingApplied = false;
        foreach (var renderer in visuals) if (renderer) renderer.enabled = false;
        foreach (var renderer in bookVisuals) if (renderer) renderer.enabled = false;
    }
    void Clear()
    {
        RestoreRendering(); ready = false; activeBook = null;
        if (visualRoot) Destroy(visualRoot); if (bookRoot) Destroy(bookRoot);
        foreach (var mesh in meshes) if (mesh) Destroy(mesh);
        meshes.Clear(); bones.Clear(); skins.Clear(); visuals.Clear(); bookVisuals.Clear();
        worldBookRenderers = null;
    }
}
