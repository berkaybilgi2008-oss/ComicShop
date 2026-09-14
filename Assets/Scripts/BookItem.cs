using System.Collections.Generic;
using UnityEngine;

public class BookItem : MonoBehaviour
{
    [Header("Kitap Kimligi")]
    [Min(0)] public int bookID;
    [Min(0)] public int brandID;

    [Header("Oyundaki Isim")]
    [Tooltip("Bu kitap icin kullanilacak gorunen isim. Bos ise Book ID kullanilir.")]
    public string displayName;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? $"Book {bookID + 1}" : displayName;

    [Header("Kenar (Outline) Highlight Ayarlari")]
    public Material outlineMaterial;
    public float outlineScale = 1.05f;

    [Header("Kapak Gorseli")]
    public Renderer coverRenderer;

    [Header("Kitap Temel Rotasyonu")]
    [Tooltip("Bu kitabin elde ve rafta kullanilacak temel rotasyonu. FBX'in Unity'de sahneye suruklendigindeki acisindan baslar; buradan elle degistirebilirsin.")]
    public Vector3 baseRotationEuler;

    [HideInInspector] public Quaternion nativeRotation = Quaternion.identity;
    [HideInInspector] public Quaternion orientationCorrection = Quaternion.identity;

    private GameObject[] outlineObjects;
    private Vector3 originalScale;

    public ShelfSlot currentSlot;
    public bool IsHeld { get; private set; }
    public Vector3 OriginalScale => originalScale;
    public Quaternion NativeRotation => Quaternion.Euler(baseRotationEuler);

    [Header("Birakma Fizigi")]
    public float sleepLinearVelocity = 0.03f;
    public float sleepAngularVelocity = 0.03f;
    public float sleepDelay = 0.25f;
    private float stillTimer;
    private Rigidbody body;
    private Collider physicsCollider;
    private readonly ContactPoint[] settlingContacts = new ContactPoint[32];
    private int settlingContactCount;
    private float contactStep = float.NegativeInfinity;
    private float edgeAssistUntil;
    private Vector3 edgeAssistTarget;
    private int impactTipAttempts;
    private float settleNotBefore;
    private readonly HashSet<BookItem> supportVisited = new HashSet<BookItem>();
    private bool frozenAtRest;
    private float nextSupportCheck;
    private struct RestSupport
    {
        public Collider collider;
        public Vector3 position, scale;
        public Quaternion rotation;
    }
    private readonly List<RestSupport> restSupports = new List<RestSupport>(8);

    private bool CaptureRestSupports()
    {
        restSupports.Clear();
        if (physicsCollider == null || Physics.gravity.sqrMagnitude < 0.0001f) return false;
        Vector3 up = -Physics.gravity.normalized;
        for (int i = 0; i < settlingContactCount; i++)
        {
            var contact = settlingContacts[i];
            var support = contact.otherCollider;
            if (support == null || support.attachedRigidbody == body || support.isTrigger || !support.enabled || !support.gameObject.activeInHierarchy || Vector3.Dot(contact.normal, up) < 0.5f) continue;
            if (support.GetComponentInParent<PlayerInteraction>() != null) continue;
            var other = support.GetComponentInParent<BookItem>();
            if (other != null && (other.IsHeld || (other.currentSlot == null && !other.frozenAtRest))) continue;
            var supportBody = support.attachedRigidbody;
            if (supportBody != null && (!supportBody.isKinematic || !supportBody.detectCollisions)) continue;
            const float toleranceSquared = 0.025f * 0.025f;
            if ((support.ClosestPoint(contact.point) - contact.point).sqrMagnitude > toleranceSquared || (physicsCollider.ClosestPoint(contact.point) - contact.point).sqrMagnitude > toleranceSquared) continue;
            bool duplicate = false;
            foreach (var saved in restSupports) if (saved.collider == support) { duplicate = true; break; }
            if (!duplicate) restSupports.Add(new RestSupport { collider = support, position = support.transform.position, rotation = support.transform.rotation, scale = support.transform.lossyScale });
        }
        return restSupports.Count > 0;
    }

    private void CheckFrozenSupport()
    {
        if (!frozenAtRest || Time.time < nextSupportCheck) return;
        nextSupportCheck = Time.time + 0.1f;
        bool intact = restSupports.Count > 0;
        foreach (var saved in restSupports)
        {
            var support = saved.collider;
            if (support == null || !support.enabled || !support.gameObject.activeInHierarchy || support.isTrigger) { intact = false; break; }
            var supportBody = support.attachedRigidbody;
            var other = support.GetComponentInParent<BookItem>();
            if ((supportBody != null && (!supportBody.isKinematic || !supportBody.detectCollisions)) || (other != null && other.IsHeld) || (support.transform.position - saved.position).sqrMagnitude > 0.000001f || Quaternion.Angle(support.transform.rotation, saved.rotation) > 0.1f || (support.transform.lossyScale - saved.scale).sqrMagnitude > 0.000001f) { intact = false; break; }
        }
        if (intact) return;
        frozenAtRest = false; restSupports.Clear(); body.isKinematic = false; body.useGravity = true; body.detectCollisions = true; body.WakeUp(); stillTimer = 0f; settlingContactCount = 0; contactStep = float.NegativeInfinity; settleNotBefore = Time.time + 0.5f;
    }

    [Header("Elde Tutulan Kitap Kontrolu")]
    [Tooltip("Bir kitabin altindaki elde tasinan kitabi algilamak icin kullanilan dikey tolerans.")]
    [Min(0.005f)] public float heldSupportTolerance = 0.08f;

    private enum SupportState { None, Stable, Held }

    void Awake()
    {
        originalScale = transform.localScale; outlineObjects = null; body = GetComponent<Rigidbody>(); physicsCollider = GetComponentInChildren<Collider>();
    }

    void FixedUpdate()
    {
        var manager = Unity.Netcode.NetworkManager.Singleton;
        if (manager != null && manager.IsListening && !manager.IsServer) return;
        CheckFrozenSupport();
        if (IsHeld || currentSlot != null || body == null || body.isKinematic) return;
        if (ContinueEdgeSettling()) return;
        if (Time.time < settleNotBefore) return;
        if (body.linearVelocity.sqrMagnitude > sleepLinearVelocity * sleepLinearVelocity || body.angularVelocity.sqrMagnitude > sleepAngularVelocity * sleepAngularVelocity) { stillTimer = 0f; return; }
        stillTimer += Time.fixedDeltaTime;
        if (stillTimer < Mathf.Max(0.5f, sleepDelay)) return;
        stillTimer = 0f;
        if (!CaptureRestSupports()) return;
        if (!CanFreezeAfterSettling()) return;
        body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.isKinematic = true; frozenAtRest = true; nextSupportCheck = Time.time + 0.1f;
    }

    void OnCollisionEnter(Collision collision) { RecordSettlingContacts(collision); AssistEdgeImpact(collision); }
    void OnCollisionStay(Collision collision) { RecordSettlingContacts(collision); }
    void OnCollisionExit(Collision collision) { settlingContactCount = 0; contactStep = float.NegativeInfinity; stillTimer = 0f; }

    private void AssistEdgeImpact(Collision collision)
    {
        if (IsHeld || currentSlot != null || body == null || body.isKinematic || physicsCollider == null || impactTipAttempts >= 1 || Physics.gravity.sqrMagnitude < 0.0001f) return;
        Vector3 up = -Physics.gravity.normalized; bool hitSupportingSurface = false;
        for (int i = 0; i < collision.contactCount; i++) if (Mathf.Abs(Vector3.Dot(collision.GetContact(i).normal, up)) >= 0.6f) { hitSupportingSurface = true; break; }
        if (!hitSupportingSurface) return;
        float upwardSpeed = Vector3.Dot(body.linearVelocity, up); if (upwardSpeed > 0f) body.linearVelocity -= up * (upwardSpeed * 0.85f);
        ResolveLocalAxes(); Vector3 cover = transform.TransformDirection(localCoverNormal).normalized; float faceUp = Vector3.Dot(cover, up); if (Mathf.Abs(faceUp) >= 0.6f) return;
        Vector3 targetNormal = faceUp >= 0f ? up : -up; Vector3 torqueAxis = Vector3.Cross(cover, targetNormal); if (torqueAxis.sqrMagnitude < 0.0001f) return;
        body.WakeUp(); body.AddTorque(torqueAxis.normalized * 0.75f, ForceMode.VelocityChange); impactTipAttempts++; stillTimer = 0f; settleNotBefore = Time.time + 0.75f;
    }

    private void RecordSettlingContacts(Collision collision)
    {
        if (IsHeld || currentSlot != null || body == null || body.isKinematic) return;
        if (contactStep != Time.fixedTime) { contactStep = Time.fixedTime; settlingContactCount = 0; }
        for (int i = 0; i < collision.contactCount && settlingContactCount < settlingContacts.Length; i++) settlingContacts[settlingContactCount++] = collision.GetContact(i);
    }

    private bool CanFreezeAfterSettling()
    {
        if (settlingContactCount == 0 || (!body.IsSleeping() && Time.fixedTime - contactStep > Time.fixedDeltaTime * 2.5f)) { body.WakeUp(); return false; }
        if (physicsCollider == null || Physics.gravity.sqrMagnitude < 0.0001f) return true;
        ResolveLocalAxes(); Vector3 up = -Physics.gravity.normalized; Vector3 cover = transform.TransformDirection(localCoverNormal).normalized; Vector3 fall = Vector3.ProjectOnPlane(cover, up); if (fall.sqrMagnitude < 0.5f) return true; fall.Normalize(); Vector3 center = body.worldCenterOfMass; float minSupport = float.PositiveInfinity; float maxSupport = float.NegativeInfinity; float supportHeight = 0f; int supports = 0; bool braced = false;
        float height = Vector3.Dot(physicsCollider.bounds.extents, new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z))) * 2f;
        for (int i = 0; i < settlingContactCount; i++) { ContactPoint contact = settlingContacts[i]; Vector3 offset = contact.point - center; float vertical = Vector3.Dot(offset, up); float normalUp = Vector3.Dot(contact.normal, up); if (vertical > -height * 0.2f && Mathf.Abs(normalUp) < 0.7f) braced = true; if (normalUp < 0.5f || vertical >= 0f) continue; float position = Vector3.Dot(offset, fall); minSupport = Mathf.Min(minSupport, position); maxSupport = Mathf.Max(maxSupport, position); supportHeight += -vertical; supports++; }
        if (supports == 0) { body.WakeUp(); return false; }
        if (braced) return true;
        float lever = supportHeight / supports; bool outsideSupport = minSupport > 0.002f || maxSupport < -0.002f; bool narrowEdge = maxSupport - minSupport < lever * 0.6f; if (!outsideSupport && !narrowEdge) return true;
        float direction; if (outsideSupport) direction = minSupport > 0f ? -1f : 1f; else { float faceUp = Vector3.Dot(cover, up); direction = faceUp < 0f ? 1f : -1f; }
        edgeAssistTarget = direction < 0f ? up : -up; edgeAssistUntil = Time.time + 2f; ContinueEdgeSettling(); return false;
    }

    private bool ContinueEdgeSettling()
    {
        if (edgeAssistUntil <= 0f) return false;
        Vector3 cover = transform.TransformDirection(localCoverNormal).normalized; bool hasRecentContact = settlingContactCount > 0 && (body.IsSleeping() || Time.fixedTime - contactStep <= Time.fixedDeltaTime * 2.5f);
        if (Time.time >= edgeAssistUntil || !hasRecentContact || Vector3.Dot(cover, edgeAssistTarget) >= 0.8f) { edgeAssistUntil = 0f; stillTimer = 0f; settleNotBefore = Time.time + 0.25f; return false; }
        Vector3 axis = Vector3.Cross(cover, edgeAssistTarget).normalized; float speed = Vector3.Dot(body.angularVelocity, axis); float acceleration = Mathf.Clamp((2f - speed) * 12f, 0f, 24f); body.WakeUp(); body.AddTorque(axis * acceleration, ForceMode.Acceleration); stillTimer = 0f; return true;
    }

    SupportState GetSupportState() { supportVisited.Clear(); return GetSupportStateRecursive(this, supportVisited); }

    SupportState GetSupportStateRecursive(BookItem book, HashSet<BookItem> visited)
    {
        if (book == null || !visited.Add(book)) return SupportState.None;
        if (book.IsHeld) return SupportState.Held;
        Collider ownCollider = book.GetComponentInChildren<Collider>(); if (ownCollider == null) return SupportState.None;
        Bounds ownBounds = ownCollider.bounds; float tolerance = heldSupportTolerance; bool foundUnstableBookSupport = false;
        Vector3 probeCenter = new Vector3(ownBounds.center.x, ownBounds.min.y + tolerance * 0.5f, ownBounds.center.z); Vector3 probeHalfExtents = new Vector3(Mathf.Max(0.005f, ownBounds.extents.x * 0.95f), tolerance * 0.5f, Mathf.Max(0.005f, ownBounds.extents.z * 0.95f));
        Collider[] candidates = Physics.OverlapBox(probeCenter, probeHalfExtents, Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < candidates.Length; i++) { Collider candidate = candidates[i]; if (candidate == null || candidate == ownCollider) continue; BookItem otherBook = candidate.GetComponentInParent<BookItem>(); if (otherBook == book) continue; Bounds candidateBounds = candidate.bounds; if (candidateBounds.max.y < ownBounds.min.y - tolerance || candidateBounds.min.y > ownBounds.min.y + tolerance) continue; if (candidateBounds.max.x < ownBounds.min.x || candidateBounds.min.x > ownBounds.max.x || candidateBounds.max.z < ownBounds.min.z || candidateBounds.min.z > ownBounds.max.z) continue; if (otherBook != null) { SupportState otherState = GetSupportStateRecursive(otherBook, visited); if (otherState == SupportState.Held) return SupportState.Held; if (otherState == SupportState.Stable) continue; foundUnstableBookSupport = true; } }
        return foundUnstableBookSupport ? SupportState.None : SupportState.Stable;
    }

    private Vector3 localCoverNormal;
    private bool axesResolved;
    private void ResolveLocalAxes()
    {
        if (axesResolved) return;
        localCoverNormal = Vector3.forward;
        if (coverRenderer != null)
        {
            Vector3 center = coverRenderer.bounds.center;
            Vector3[] candidates = { transform.right, transform.up, transform.forward };
            float best = -1f;
            foreach (Vector3 axis in candidates) { float score = Mathf.Abs(Vector3.Dot(axis, Vector3.up)); if (score > best) { best = score; localCoverNormal = transform.InverseTransformDirection(axis); } }
        }
        axesResolved = true;
    }

    public void SetHeld(bool held)
    {
        IsHeld = held;
        if (held) { frozenAtRest = false; restSupports.Clear(); edgeAssistUntil = 0f; stillTimer = 0f; if (body != null) { body.isKinematic = true; body.useGravity = false; body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; } }
        else if (body != null && currentSlot == null) { body.isKinematic = false; body.useGravity = true; body.WakeUp(); stillTimer = 0f; settleNotBefore = Time.time + 0.25f; }
    }

    public void SetCurrentSlot(ShelfSlot slot) { currentSlot = slot; if (slot != null && body != null) { frozenAtRest = false; restSupports.Clear(); body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.isKinematic = true; body.useGravity = false; } }

    public void SnapToSlot(Transform slotTransform)
    {
        transform.SetPositionAndRotation(slotTransform.position, slotTransform.rotation * NativeRotation); if (body != null) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.isKinematic = true; body.useGravity = false; }
    }

    public void BeginHeldOutline() { if (outlineObjects != null && outlineObjects.Length > 0) return; if (coverRenderer == null) return; List<GameObject> outlines = new List<GameObject>(); Renderer[] renderers = GetComponentsInChildren<Renderer>(true); foreach (Renderer r in renderers) { if (r == null) continue; GameObject outline = GameObject.CreatePrimitive(PrimitiveType.Cube); outline.name = "HeldOutline"; outline.transform.SetParent(r.transform, false); outline.transform.localPosition = Vector3.zero; outline.transform.localRotation = Quaternion.identity; outline.transform.localScale = r.bounds.size * outlineScale; Renderer outlineRenderer = outline.GetComponent<Renderer>(); outlineRenderer.sharedMaterial = outlineMaterial; Collider c = outline.GetComponent<Collider>(); if (c != null) Destroy(c); outlines.Add(outline); } outlineObjects = outlines.ToArray(); }
    public void EndHeldOutline() { if (outlineObjects == null) return; foreach (GameObject obj in outlineObjects) if (obj != null) Destroy(obj); outlineObjects = null; }
    public Quaternion GetAlignedRotation(Vector3 worldUp, Vector3 heading) { Quaternion baseRotation = NativeRotation; Vector3 forward = baseRotation * Vector3.forward; Vector3 projectedForward = Vector3.ProjectOnPlane(forward, worldUp); if (projectedForward.sqrMagnitude < 0.0001f) projectedForward = Vector3.ProjectOnPlane(baseRotation * Vector3.right, worldUp); projectedForward.Normalize(); Vector3 desiredForward = Vector3.ProjectOnPlane(heading, worldUp).normalized; Quaternion yaw = Quaternion.FromToRotation(projectedForward, desiredForward); return yaw * baseRotation; }
}
