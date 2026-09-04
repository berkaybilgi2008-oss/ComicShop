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

    void Awake()
    {
        originalScale = transform.localScale;
        CreateOutlineObjects();
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

            // Normal durumda kitap sabitlenebilir. Tek istisna, destek zincirinin
            // herhangi bir yerinde su anda oyuncunun elinde olan bir kitap varsa
            // sabitlenmez. Boylece A elde, B A'nin ustunde, C de B'nin ustundeyken
            // C'nin havada sabitlenmesi engellenir.
            if (stillTimer >= sleepDelay && !IsSupportedByHeldBook())
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

    bool IsSupportedByHeldBook()
    {
        HashSet<BookItem> visited = new HashSet<BookItem>();
        return IsSupportedByHeldBookRecursive(this, visited);
    }

    bool IsSupportedByHeldBookRecursive(BookItem book, HashSet<BookItem> visited)
    {
        if (book == null || !visited.Add(book))
            return false;

        if (book.IsHeld)
            return true;

        Collider ownCollider = book.GetComponentInChildren<Collider>();
        if (ownCollider == null)
            return false;

        Bounds ownBounds = ownCollider.bounds;
        float tolerance = heldSupportTolerance;

        // Sadece kitabin alt tarafini tarariz. Bu bir pozisyon duzeltmesi
        // degildir; mevcut destek zincirini tespit etmek icindir.
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
            if (otherBook == null || otherBook == book)
                continue;

            Bounds otherBounds = candidate.bounds;

            // Aday kitap gercekten bu kitabin altinda/temas bolgesinde olmali.
            // Boylece yandaki veya ustteki kitaplar destek zincirine girmez.
            if (otherBounds.max.y < ownBounds.min.y - tolerance)
                continue;

            if (otherBounds.min.y > ownBounds.min.y + tolerance)
                continue;

            if (otherBounds.max.x < ownBounds.min.x ||
                otherBounds.min.x > ownBounds.max.x ||
                otherBounds.max.z < ownBounds.min.z ||
                otherBounds.min.z > ownBounds.max.z)
                continue;

            // Dogrudan eldeki kitap varsa zincir kirilir: sabitlenme yok.
            if (otherBook.IsHeld)
                return true;

            // Aradaki kitap da fiziksel olarak bu kitabi destekliyorsa zinciri
            // asagi dogru takip ederiz. Boylece 3., 4., 5. kitaplar da eldeki
            // kitabin dolayli destegini dogru algilar.
            if (IsSupportedByHeldBookRecursive(otherBook, visited))
                return true;
        }

        return false;
    }

    public void SetCoverMaterial(Material coverMaterial)
    {
        if (coverRenderer != null && coverMaterial != null)
            coverRenderer.material = coverMaterial;
    }

    void CreateOutlineObjects()
    {
        if (outlineMaterial == null)
            return;

        MeshFilter[] sourceMeshes = GetComponentsInChildren<MeshFilter>(true);
        outlineObjects = new GameObject[sourceMeshes.Length];

        for (int i = 0; i < sourceMeshes.Length; i++)
        {
            MeshFilter source = sourceMeshes[i];
            if (source.sharedMesh == null)
                continue;

            GameObject outline = new GameObject("Outline_" + i);
            outline.transform.SetParent(source.transform, false);
            outline.transform.localScale = Vector3.one * outlineScale;

            MeshFilter mf = outline.AddComponent<MeshFilter>();
            mf.sharedMesh = source.sharedMesh;

            MeshRenderer mr = outline.AddComponent<MeshRenderer>();
            mr.sharedMaterial = outlineMaterial;
            outline.SetActive(false);
            outlineObjects[i] = outline;
        }
    }

    public void SetHighlight(bool on)
    {
        if (currentSlot != null || IsHeld)
            on = false;

        if (outlineObjects == null)
            return;

        foreach (GameObject outline in outlineObjects)
        {
            if (outline != null)
                outline.SetActive(on);
        }
    }

    public void SetHeld(bool held)
    {
        IsHeld = held;

        if (held)
            SetHighlight(false);

        // Collider her durumda aktif kalir. Elde iken oyuncuyla carpismayi
        // PlayerInteraction, Physics.IgnoreCollision ile kapatir.
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
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = held;
            rb.interpolation = held ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }
    }
}
