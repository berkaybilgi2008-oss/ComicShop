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
                armVertex[i] = bone == lower || bone.IsChildOf(lower);
                bool head = false;
                for (Transform t = bone; t && t != rig.transform; t = t.parent)
                    if (t.name == "Head" || t.name == "Neck") head = true;
                hideVertex[i] = bone == upper || bone.IsChildOf(upper) || head;
                // The camera must not see the open neck/shoulder cut of the body.
                if (bone.name == "Chest" || bone.name == "Spine") hideVertex[i] = true;
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
                    if ((armVertex[x] ? 1 : 0) + (armVertex[y] ? 1 : 0) + (armVertex[z] ? 1 : 0) >= 2)
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
        float l1 = Vector3.Distance(a.position, b.position), l2 = Vector3.Distance(b.position, c.position);
        // Compose the FOREARM, not the book center: elbow below the frame and
        // wrist near the drawn upper-left point. Common depth preserves bone length.
        Vector3 elbowRay = camera.ViewportToWorldPoint(new Vector3(left ? 0.35f : 0.65f,
            -0.16f, 1f)) - view.position;
        Vector3 wristRay = camera.ViewportToWorldPoint(new Vector3(left ? 0.23f : 0.77f, 0.58f + charge * 0.05f, 1f)) - view.position;
        float depth = l2 / Mathf.Max(0.0001f, (wristRay - elbowRay).magnitude);
        if (depth < camera.nearClipPlane + 0.08f)
        {
            // Uniform presentation scale preserves anatomy at large near planes.
            float ratio = (camera.nearClipPlane + 0.08f) / Mathf.Max(0.001f, depth);
            visualRoot.transform.localScale *= ratio;
            l1 *= ratio; l2 *= ratio;
            depth = camera.nearClipPlane + 0.08f;
        }
        Vector3 wrist = view.position + wristRay * depth;
        Vector3 lower = (wristRay - elbowRay).normalized;
        // On release retain the original cubic timing and move forward in one stroke.
        lower = Vector3.Slerp(lower, view.forward, release * 0.85f);
        wrist += view.forward * (release * 0.18f) - view.up * (release * 0.10f);
        wrist += inventory.CurrentThrowShake;
        Vector3 elbow = wrist - lower * l2;
        Vector3 upper = Vector3.ProjectOnPlane(view.forward, lower).normalized;
        upper = (upper * Mathf.Sin(70f * Mathf.Deg2Rad) - lower * Mathf.Cos(70f * Mathf.Deg2Rad)).normalized;

        Quaternion rotation = book.GetAlignedRotation(
            -view.forward * 0.9f + view.right * side * 0.4f,
            Quaternion.AngleAxis(release * 35f, view.right) * view.up);
        Vector3 scale = book.OriginalScale * (inventory.chargeScaleMultiplier * 0.65f);
        float enter = Mathf.SmoothStep(0f, 1f, (Time.time - enteredAt) / Mathf.Max(0.01f, inventory.chargeEnterDuration));
        rotation = Quaternion.Slerp(entryRotation, rotation, enter);
        scale = Vector3.Lerp(entryScale, scale, enter);
        rotation = Quaternion.Slerp(rotation, book.transform.rotation, release);
        scale = Vector3.Lerp(scale, book.transform.lossyScale, release);
        book.GetAxisFrame(out Vector3 cover, out Vector3 along, out Vector3 wide, out Vector3 half);
        half *= scale.magnitude / Mathf.Max(0.0001f, book.OriginalScale.magnitude);
        cover = rotation * cover; along = rotation * along; wide = rotation * wide;
        if (Vector3.Dot(wide, view.right * -side) < 0f) wide = -wide;
        Vector3 bookOffset = along * (half.y * 0.92f) - wide * (half.z * 0.86f) - cover * half.x;
        Quaternion grip = Quaternion.LookRotation(-cover, -along);
        Vector3 gripOffset = Vector3.Scale(new Vector3(0f, -0.06f, 0.025f), c.lossyScale);
        Vector3 entryWrist = entryPosition - bookOffset - grip * gripOffset;
        wrist = Vector3.Lerp(entryWrist, wrist, enter);
        Vector3 releaseWrist = book.transform.position - bookOffset - grip * gripOffset;
        wrist = Vector3.Lerp(wrist, releaseWrist, release);
        elbow = wrist - lower * l2;
        // Move the complete presentation skeleton, preserving every bind offset.
        visualRoot.transform.position += elbow - upper * l1 - a.position;
        a.rotation = Quaternion.FromToRotation(b.position - a.position, elbow - a.position) * a.rotation;
        b.rotation = Quaternion.FromToRotation(c.position - b.position, wrist - b.position) * b.rotation;
        c.rotation = Quaternion.RotateTowards(b.rotation * wristRest, grip, 40f);
        Vector3 palm = c.position + c.rotation * gripOffset;
        bookRoot.transform.SetPositionAndRotation(palm + bookOffset, rotation);
        bookRoot.transform.localScale = scale;
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
