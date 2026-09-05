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

    [Header("Elde Tutulan Kitap Kontrolu")]
    [Tooltip("Bir kitabin altindaki elde tasinan kitabi algilamak icin kullanilan dikey tolerans.")]
    [Min(0.005f)] public float heldSupportTolerance = 0.08f;

    private enum SupportState
    {
        None,
        Stable,
        Held
    }

    void Awake()
    {
        originalScale = transform.localScale;
        // Outline objeleri artik olusturulmuyor. Highlight sistemi tamamen kapali.
        outlineObjects = null;
    }

    void Update()
    {
        if (IsHeld)
            return;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null || rb.isKinematic)
            return;

        if (rb.linearVelocity.sqrMagnitude <= sleepLinearVelocity * sleepLinearVelocity &&
            rb.angularVelocity.sqrMagnitude <= sleepAngularVelocity * sleepAngularVelocity)
        {
            stillTimer += Time.deltaTime;

            SupportState supportState = GetSupportState();

            if (stillTimer >= sleepDelay && supportState == SupportState.Stable)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.Sleep();
                stillTimer = 0f;
            }
        }
        else
        {
            stillTimer = 0f;
        }
    }

    SupportState GetSupportState()
    {
        HashSet<BookItem> visited = new HashSet<BookItem>();
        return GetSupportStateRecursive(this, visited);
    }

    SupportState GetSupportStateRecursive(BookItem book, HashSet<BookItem> visited)
    {
        if (book == null || !visited.Add(book))
            return SupportState.None;

        if (book.IsHeld)
            return SupportState.Held;

        Collider ownCollider = book.GetComponentInChildren<Collider>();
        if (ownCollider == null)
            return SupportState.None;

        Bounds ownBounds = ownCollider.bounds;
        float tolerance = heldSupportTolerance;
        bool foundUnstableBookSupport = false;

        Vector3 probeCenter = new Vector3(
            ownBounds.center.x,
            ownBounds.min.y + tolerance * 0.5f,
            ownBounds.center.z);

        Vector3 probeHalfExtents = new Vector3(
            Mathf.Max(0.005f, ownBounds.extents.x * 0.95f),
            tolerance * 0.5f,
            Mathf.Max(0.005f, ownBounds.extents.z * 0.95f));

        Collider[] candidates = Physics.OverlapBox(
            probeCenter,
            probeHalfExtents,
            Quaternion.identity,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < candidates.Length; i++)
        {
            Collider candidate = candidates[i];
            if (candidate == null || candidate == ownCollider)
                continue;

            BookItem otherBook = candidate.GetComponentInParent<BookItem>();
            if (otherBook == book)
                continue;

            Bounds candidateBounds = candidate.bounds;

            if (candidateBounds.max.y < ownBounds.min.y - tolerance)
                continue;

            if (candidateBounds.min.y > ownBounds.min.y + tolerance)
                continue;

            if (candidateBounds.max.x < ownBounds.min.x ||
                candidateBounds.min.x > ownBounds.max.x ||
                candidateBounds.max.z < ownBounds.min.z ||
                candidateBounds.min.z > ownBounds.max.z)
                continue;

            if (otherBook != null)
            {
                SupportState otherState = GetSupportStateRecursive(otherBook, visited);

                if (otherState == SupportState.Held)
                    return SupportState.Held;

                if (otherState == SupportState.Stable)
                    return SupportState.Stable;

                foundUnstableBookSupport = true;
                continue;
            }

            if (candidate.GetComponentInParent<PlayerInteraction>() != null)
                continue;

            return SupportState.Stable;
        }

        if (foundUnstableBookSupport)
            return SupportState.None;

        return SupportState.None;
    }

    // ------------------------------------------------------------------
    // Kitabin kendi eksenleri (mesh'ten olculur, tahmin yok)
    //
    // Bir cizgi roman yassi bir kutudur:
    //   en INCE eksen  -> kapak normali (shuriken donme ekseni)
    //   en UZUN eksen  -> boy
    // Model hangi eksende export edilmis olursa olsun bu roller degismez.
    // ------------------------------------------------------------------

    private bool axesResolved;
    private Vector3 localCoverNormal = Vector3.forward;
    private Vector3 localLongAxis = Vector3.up;
    private Vector3 localWideAxis = Vector3.right;

    private void ResolveLocalAxes()
    {
        if (axesResolved)
            return;

        axesResolved = true;

        Bounds bounds = new Bounds();
        bool found = false;
        Matrix4x4 toRoot = transform.worldToLocalMatrix;

        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter filter in filters)
        {
            if (filter == null || filter.sharedMesh == null)
                continue;

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

        if (!found)
            return;

        // Gercek olcu = mesh olcusu x kok scale (modellerde Apply Scale yapilmamis).
        Vector3 size = new Vector3(
            Mathf.Abs(bounds.size.x * originalScale.x),
            Mathf.Abs(bounds.size.y * originalScale.y),
            Mathf.Abs(bounds.size.z * originalScale.z));

        int thin = 0, longest = 0;
        for (int i = 1; i < 3; i++)
        {
            if (size[i] < size[thin]) thin = i;
            if (size[i] > size[longest]) longest = i;
        }

        if (thin == longest)
            longest = (thin + 1) % 3;

        int wide = 3 - thin - longest;

        localCoverNormal = Axis(thin);
        localLongAxis = Axis(longest);
        localWideAxis = Axis(wide);
    }

    private static Vector3 Axis(int index)
    {
        return index == 0 ? Vector3.right : index == 1 ? Vector3.up : Vector3.forward;
    }

    /// <summary>
    /// Kitabi istenen yone hizalayan DUNYA rotasyonu.
    /// coverNormal: kapaklarin bakacagi yon (shuriken donme ekseni)
    /// longAxis   : kitabin boyunun bakacagi yon
    /// Ikisi de birbirine dik olmali.
    /// </summary>
    public Quaternion GetAlignedRotation(Vector3 coverNormal, Vector3 longAxis)
    {
        ResolveLocalAxes();

        if (coverNormal.sqrMagnitude < 0.0001f || longAxis.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        coverNormal = coverNormal.normalized;
        longAxis = Vector3.ProjectOnPlane(longAxis, coverNormal).normalized;

        if (longAxis.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        float handedness = Mathf.Sign(
            Vector3.Dot(Vector3.Cross(localCoverNormal, localLongAxis), localWideAxis));

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

        // Atis/sarj pozunda su anda arka kapak gorunuyordu.
        // Kitabin boy ekseni etrafinda 180 derece cevirerek on kapagi kameraya getiriyoruz.
        return Quaternion.AngleAxis(180f, longAxis) * alignedRotation;
    }

    public void SetCoverMaterial(Material coverMaterial)
    {
        if (coverRenderer != null && coverMaterial != null)
            coverRenderer.material = coverMaterial;
    }

    public void SetHighlight(bool on)
    {
        // Outline highlight sistemi kaldirildi.
    }

    public void SetHeld(bool held)
    {
        IsHeld = held;

        if (held)
            SetHighlight(false);

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            if (col != null)
                col.enabled = true;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            stillTimer = 0f;

            // Kinematic bir Rigidbody'nin hizi yazilamaz -- Unity uyari basar.
            // Bu yuzden once kinematic durumunu ayarliyoruz, hizi sadece fizik
            // ACIKKEN sifirliyoruz.
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