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
    [Tooltip("Kitabin elde ve rafta kullanilacak temel rotasyonu. FBX'in native rotasyonudur.")]
    [HideInInspector] public Quaternion nativeRotation = Quaternion.identity;

    // Eski prefablarin baseRotationEuler alanini korumak icin tutulur.
    [HideInInspector] public Vector3 baseRotationEuler;
    [HideInInspector] public Quaternion orientationCorrection = Quaternion.identity;

    private Vector3 originalScale;

    public ShelfSlot currentSlot;
    public bool IsHeld { get; private set; }
    public Vector3 OriginalScale => originalScale;
    public Quaternion NativeRotation => nativeRotation;

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
        // Eski prefablar baseRotationEuler ile uretilmisse, yalnizca nativeRotation
        // default ise eski degeri geriye uyumlu sekilde kullan.
        if (nativeRotation == Quaternion.identity && baseRotationEuler != Vector3.zero)
            nativeRotation = Quaternion.Euler(baseRotationEuler);

        // VERIDIAN kitaplarinda mevcut authored X acisina gore hedef rotasyonu uygula.
        // X -4... ile baslayan eski degerler -> (270, 0, 180)
        // X 270... olan eski degerler -> (0, 0, 180)
        if (brandID == 0)
        {
            if (baseRotationEuler.x < 0f)
            {
                baseRotationEuler = new Vector3(270f, 0f, 180f);
                nativeRotation = Quaternion.Euler(baseRotationEuler);
            }
            else if (Mathf.Approximately(baseRotationEuler.x, 270f))
            {
                baseRotationEuler = new Vector3(0f, 0f, 180f);
                nativeRotation = Quaternion.Euler(baseRotationEuler);
            }
        }
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

        Vector3 probeCenter = new Vector3(ownBounds.center.x, ownBounds.min.y + tolerance * 0.5f, ownBounds.center.z);
        Vector3 probeHalfExtents = new Vector3(Mathf.Max(0.005f, ownBounds.extents.x * 0.95f), tolerance * 0.5f, Mathf.Max(0.005f, ownBounds.extents.z * 0.95f));

        Collider[] candidates = Physics.OverlapBox(probeCenter, probeHalfExtents, Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < candidates.Length; i++)
        {
            Collider candidate = candidates[i];
            if (candidate == null || candidate == ownCollider)
                continue;

            BookItem otherBook = candidate.GetComponentInParent<BookItem>();
            if (otherBook == book)
                continue;

            Bounds candidateBounds = candidate.bounds;
            if (candidateBounds.max.y < ownBounds.min.y - tolerance || candidateBounds.min.y > ownBounds.min.y + tolerance)
                continue;
            if (candidateBounds.max.x < ownBounds.min.x || candidateBounds.min.x > ownBounds.max.x || candidateBounds.max.z < ownBounds.min.z || candidateBounds.min.z > ownBounds.max.z)
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

        return foundUnstableBookSupport ? SupportState.None : SupportState.None;
    }

    public void SetCoverMaterial(Material coverMaterial)
    {
        if (coverRenderer != null && coverMaterial != null)
            coverRenderer.material = coverMaterial;
    }

    public void SetHighlight(bool on) { }

    public void SetHeld(bool held)
    {
        IsHeld = held;
        if (held)
            SetHighlight(false);

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
            if (col != null) col.enabled = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            stillTimer = 0f;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = held;
            rb.interpolation = held ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }
    }
}
