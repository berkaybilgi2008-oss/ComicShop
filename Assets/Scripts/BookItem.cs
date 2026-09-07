using System.Collections.Generic;
using UnityEngine;

public class BookItem : MonoBehaviour
{
    [Header("Kitap Kimligi")]
    [Min(0)] public int bookID;
    [Min(0)] public int brandID;

    public string DisplayName => $"Book {bookID + 1}";

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
    // Fixed storage: no contact-array allocation or scene scan per physics step.
    private readonly ContactPoint[] settlingContacts = new ContactPoint[32];
    private int settlingContactCount;
    private float contactStep = float.NegativeInfinity;
    private float edgeAssistUntil;
    private Vector3 edgeAssistTarget;
    private int impactTipAttempts;
    private float settleNotBefore;
    private readonly HashSet<BookItem> supportVisited = new HashSet<BookItem>();

    [Header("Elde Tutulan Kitap Kontrolu")]
    [Tooltip("Bir kitabin altindaki elde tasinan kitabi algilamak icin kullanilan dikey tolerans.")]
    [Min(0.005f)] public float heldSupportTolerance = 0.08f;

    private enum SupportState { None, Stable, Held }

    void Awake()
    {
        originalScale = transform.localScale;
        outlineObjects = null;
        body = GetComponent<Rigidbody>();
        physicsCollider = GetComponentInChildren<Collider>();
    }

    void FixedUpdate()
    {
        if (IsHeld || currentSlot != null || body == null || body.isKinematic) return;
        if (ContinueEdgeSettling()) return;
        if (Time.time < settleNotBefore) return;

        if (body.linearVelocity.sqrMagnitude > sleepLinearVelocity * sleepLinearVelocity ||
            body.angularVelocity.sqrMagnitude > sleepAngularVelocity * sleepAngularVelocity)
        {
            stillTimer = 0f;
            return;
        }

        stillTimer += Time.fixedDeltaTime;
        if (stillTimer < Mathf.Max(0.5f, sleepDelay)) return;
        stillTimer = 0f;
        // Only query the existing support chain when actually ready to freeze.
        if (GetSupportState() != SupportState.Stable) return;
        if (!CanFreezeAfterSettling()) return;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        RecordSettlingContacts(collision);
        AssistEdgeImpact(collision);
    }
    void OnCollisionStay(Collision collision) { RecordSettlingContacts(collision); }
    void OnCollisionExit(Collision collision)
    {
        settlingContactCount = 0;
        contactStep = float.NegativeInfinity;
        stillTimer = 0f;
    }

    private void AssistEdgeImpact(Collision collision)
    {
        if (IsHeld || currentSlot != null || body == null || body.isKinematic ||
            physicsCollider == null || impactTipAttempts >= 1 || Physics.gravity.sqrMagnitude < 0.0001f)
            return;

        Vector3 up = -Physics.gravity.normalized;
        bool hitSupportingSurface = false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (Mathf.Abs(Vector3.Dot(collision.GetContact(i).normal, up)) >= 0.6f)
            {
                hitSupportingSurface = true;
                break;
            }
        }
        if (!hitSupportingSurface) return;

        // Books absorb most of the upward rebound from floors and piles.
        float upwardSpeed = Vector3.Dot(body.linearVelocity, up);
        if (upwardSpeed > 0f)
            body.linearVelocity -= up * (upwardSpeed * 0.85f);

        ResolveLocalAxes();
        Vector3 cover = transform.TransformDirection(localCoverNormal).normalized;
        float faceUp = Vector3.Dot(cover, up);
        // The cover is steeper than about 53 degrees: it landed on an edge.
        if (Mathf.Abs(faceUp) >= 0.6f) return;

        Vector3 targetNormal = faceUp >= 0f ? up : -up;
        Vector3 torqueAxis = Vector3.Cross(cover, targetNormal);
        if (torqueAxis.sqrMagnitude < 0.0001f) return;

        body.WakeUp();
        body.AddTorque(torqueAxis.normalized * 0.75f, ForceMode.VelocityChange);
        impactTipAttempts++;
        stillTimer = 0f;
        settleNotBefore = Time.time + 0.75f;
    }

    private void RecordSettlingContacts(Collision collision)
    {
        if (IsHeld || currentSlot != null || body == null || body.isKinematic) return;
        if (contactStep != Time.fixedTime)
        {
            contactStep = Time.fixedTime;
            settlingContactCount = 0;
        }
        for (int i = 0; i < collision.contactCount && settlingContactCount < settlingContacts.Length; i++)
            settlingContacts[settlingContactCount++] = collision.GetContact(i);
    }

    private bool CanFreezeAfterSettling()
    {
        // Sleeping bodies stop sending Stay; their last contacts remain useful.
        if (settlingContactCount == 0 ||
            (!body.IsSleeping() && Time.fixedTime - contactStep > Time.fixedDeltaTime * 2.5f))
        {
            body.WakeUp();
            return false;
        }
        if (physicsCollider == null || Physics.gravity.sqrMagnitude < 0.0001f) return true;
        ResolveLocalAxes();
        Vector3 up = -Physics.gravity.normalized;
        Vector3 cover = transform.TransformDirection(localCoverNormal).normalized;
        Vector3 fall = Vector3.ProjectOnPlane(cover, up);
        // Broad-face resting poses need no artificial flattening.
        if (fall.sqrMagnitude < 0.5f) return true;
        fall.Normalize();
        Vector3 center = body.worldCenterOfMass;
        float minSupport = float.PositiveInfinity;
        float maxSupport = float.NegativeInfinity;
        float supportHeight = 0f;
        int supports = 0;
        bool braced = false;
        float height = Vector3.Dot(physicsCollider.bounds.extents, new Vector3(
            Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z))) * 2f;

        for (int i = 0; i < settlingContactCount; i++)
        {
            ContactPoint contact = settlingContacts[i];
            Vector3 offset = contact.point - center;
            float vertical = Vector3.Dot(offset, up);
            float normalUp = Vector3.Dot(contact.normal, up);
            // A wall/another book supporting the upper body is a legitimate lean.
            if (vertical > -height * 0.2f && Mathf.Abs(normalUp) < 0.7f)
                braced = true;
            if (normalUp < 0.5f || vertical >= 0f) continue;
            float position = Vector3.Dot(offset, fall);
            minSupport = Mathf.Min(minSupport, position);
            maxSupport = Mathf.Max(maxSupport, position);
            supportHeight += -vertical;
            supports++;
        }
        if (supports == 0) { body.WakeUp(); return false; }
        if (braced) return true;
        float lever = supportHeight / supports;
        bool outsideSupport = minSupport > 0.002f || maxSupport < -0.002f;
        bool narrowEdge = maxSupport - minSupport < lever * 0.6f;
        if (!outsideSupport && !narrowEdge) return true;

        float direction;
        if (outsideSupport) direction = minSupport > 0f ? -1f : 1f;
        else
        {
            // Tip towards the already lower face; exact vertical gets a stable tie-break.
            float faceUp = Vector3.Dot(cover, up);
            direction = faceUp < 0f ? 1f : -1f;
        }
        // Keep helping through the initial lean instead of giving up after two
        // impulses. A stuck episode ends after two seconds and rechecks contacts;
        // only a broad face or a real brace may qualify for freezing.
        edgeAssistTarget = direction < 0f ? up : -up;
        edgeAssistUntil = Time.time + 2f;
        ContinueEdgeSettling();
        return false;
    }

    private bool ContinueEdgeSettling()
    {
        if (edgeAssistUntil <= 0f) return false;
        Vector3 cover = transform.TransformDirection(localCoverNormal).normalized;
        bool hasRecentContact = settlingContactCount > 0 &&
            (body.IsSleeping() || Time.fixedTime - contactStep <= Time.fixedDeltaTime * 2.5f);
        if (Time.time >= edgeAssistUntil || !hasRecentContact ||
            Vector3.Dot(cover, edgeAssistTarget) >= 0.8f)
        {
            edgeAssistUntil = 0f;
            stillTimer = 0f;
            settleNotBefore = Time.time + 0.25f;
            return false;
        }

        Vector3 axis = Vector3.Cross(cover, edgeAssistTarget).normalized;
        float speed = Vector3.Dot(body.angularVelocity, axis);
        // Limited angular acceleration, no upward impulse or transform teleport.
        // This runs only for a resting edge book, never for the frozen population.
        float acceleration = Mathf.Clamp((2f - speed) * 12f, 0f, 24f);
        body.WakeUp();
        body.AddTorque(axis * acceleration, ForceMode.Acceleration);
        stillTimer = 0f;
        return true;
    }

    SupportState GetSupportState()
    {
        supportVisited.Clear();
        return GetSupportStateRecursive(this, supportVisited);
    }

    SupportState GetSupportStateRecursive(BookItem book, HashSet<BookItem> visited)
    {
        if (book == null || !visited.Add(book)) return SupportState.None;
        if (book.IsHeld) return SupportState.Held;

        Collider ownCollider = book.GetComponentInChildren<Collider>();
        if (ownCollider == null) return SupportState.None;

        Bounds ownBounds = ownCollider.bounds;
        float tolerance = heldSupportTolerance;
        bool foundUnstableBookSupport = false;

        Vector3 probeCenter = new Vector3(ownBounds.center.x, ownBounds.min.y + tolerance * 0.5f, ownBounds.center.z);
        Vector3 probeHalfExtents = new Vector3(Mathf.Max(0.005f, ownBounds.extents.x * 0.95f), tolerance * 0.5f, Mathf.Max(0.005f, ownBounds.extents.z * 0.95f));

        Collider[] candidates = Physics.OverlapBox(probeCenter, probeHalfExtents, Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < candidates.Length; i++)
        {
            Collider candidate = candidates[i];
            if (candidate == null || candidate == ownCollider) continue;

            BookItem otherBook = candidate.GetComponentInParent<BookItem>();
            if (otherBook == book) continue;

            Bounds candidateBounds = candidate.bounds;
            if (candidateBounds.max.y < ownBounds.min.y - tolerance || candidateBounds.min.y > ownBounds.min.y + tolerance) continue;
            if (candidateBounds.max.x < ownBounds.min.x || candidateBounds.min.x > ownBounds.max.x || candidateBounds.max.z < ownBounds.min.z || candidateBounds.min.z > ownBounds.max.z) continue;

            if (otherBook != null)
            {
                SupportState otherState = GetSupportStateRecursive(otherBook, visited);
                if (otherState == SupportState.Held) return SupportState.Held;
                if (otherState == SupportState.Stable) return SupportState.Stable;
                foundUnstableBookSupport = true;
                continue;
            }

            if (candidate.GetComponentInParent<PlayerInteraction>() != null) continue;
            return SupportState.Stable;
        }

        return foundUnstableBookSupport ? SupportState.None : SupportState.None;
    }

    private bool axesResolved;
    private Vector3 localCoverNormal = Vector3.forward;
    private Vector3 localLongAxis = Vector3.up;
    private Vector3 localWideAxis = Vector3.right;

    private void ResolveLocalAxes()
    {
        if (axesResolved) return;
        axesResolved = true;

        Bounds bounds = new Bounds();
        bool found = false;
        Matrix4x4 toRoot = transform.worldToLocalMatrix;

        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter filter in filters)
        {
            if (filter == null || filter.sharedMesh == null) continue;
            Matrix4x4 m = toRoot * filter.transform.localToWorldMatrix;
            Bounds mb = filter.sharedMesh.bounds;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    mb.center.x + ((i & 1) == 0 ? -mb.extents.x : mb.extents.x),
                    mb.center.y + ((i & 2) == 0 ? -mb.extents.y : mb.extents.y),
                    mb.center.z + ((i & 4) == 0 ? -mb.extents.z : mb.extents.z));
                Vector3 point = m.MultiplyPoint3x4(corner);
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                else bounds.Encapsulate(point);
            }
        }

        if (!found) return;

        Vector3 size = new Vector3(Mathf.Abs(bounds.size.x * originalScale.x), Mathf.Abs(bounds.size.y * originalScale.y), Mathf.Abs(bounds.size.z * originalScale.z));
        int thin = 0, longest = 0;
        for (int i = 1; i < 3; i++)
        {
            if (size[i] < size[thin]) thin = i;
            if (size[i] > size[longest]) longest = i;
        }
        if (thin == longest) longest = (thin + 1) % 3;
        int wide = 3 - thin - longest;
        localCoverNormal = Axis(thin);
        localLongAxis = Axis(longest);
        localWideAxis = Axis(wide);
    }

    private static Vector3 Axis(int index)
    {
        return index == 0 ? Vector3.right : index == 1 ? Vector3.up : Vector3.forward;
    }

    public Quaternion GetAlignedRotation(Vector3 coverNormal, Vector3 longAxis)
    {
        ResolveLocalAxes();
        if (coverNormal.sqrMagnitude < 0.0001f || longAxis.sqrMagnitude < 0.0001f) return Quaternion.identity;

        coverNormal = coverNormal.normalized;
        longAxis = Vector3.ProjectOnPlane(longAxis, coverNormal).normalized;
        if (longAxis.sqrMagnitude < 0.0001f) return Quaternion.identity;

        float handedness = Mathf.Sign(Vector3.Dot(Vector3.Cross(localCoverNormal, localLongAxis), localWideAxis));
        Vector3 wide = Vector3.Cross(coverNormal, longAxis) * handedness;

        Matrix4x4 src = Matrix4x4.identity;
        src.SetColumn(0, localCoverNormal);
        src.SetColumn(1, localLongAxis);
        src.SetColumn(2, localWideAxis);

        Matrix4x4 dst = Matrix4x4.identity;
        dst.SetColumn(0, coverNormal);
        dst.SetColumn(1, longAxis);
        dst.SetColumn(2, wide);

        Quaternion alignedRotation = (dst * src.transpose).rotation;

        // Onceki denemenin tam tersi: 180 derece ceviriyi x=0 grubuna uyguluyoruz.
        // x=270 grubunda mevcut hizalama korunuyor.
        bool needsFrontFlip = Mathf.Abs(Mathf.DeltaAngle(baseRotationEuler.x, 0f)) < 1f;
        if (needsFrontFlip)
            alignedRotation = Quaternion.AngleAxis(180f, longAxis) * alignedRotation;

        return alignedRotation;
    }

    public void SetCoverMaterial(Material coverMaterial)
    {
        if (coverRenderer != null && coverMaterial != null) coverRenderer.material = coverMaterial;
    }

    public void SetHighlight(bool on) { }

    public void SetHeld(bool held)
    {
        IsHeld = held;
        edgeAssistUntil = 0f;
        impactTipAttempts = 0;
        settleNotBefore = Time.time;
        settlingContactCount = 0;
        contactStep = float.NegativeInfinity;
        if (held) SetHighlight(false);

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders) if (col != null) col.enabled = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            stillTimer = 0f;
            if (held)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
            }
            else
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.interpolation = held ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }
    }
}