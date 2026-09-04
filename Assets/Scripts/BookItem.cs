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

    [Header("Destek Kontrolu")]
    [Tooltip("Kitabin altinda fiziksel bir destek aramak icin kullanilan dikey tolerans.")]
    [Min(0.005f)] public float supportVerticalTolerance = 0.06f;

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
            if (stillTimer >= sleepDelay && HasPhysicalSupport())
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

    bool HasPhysicalSupport()
    {
        Collider ownCollider = GetComponentInChildren<Collider>();
        if (ownCollider == null)
            return false;

        Bounds ownBounds = ownCollider.bounds;
        float tolerance = supportVerticalTolerance;

        // Kitabin alt kenarinin hemen altinda kucuk bir fizik arama bolgesi
        // olusturuyoruz. Burada herhangi bir pozisyon duzeltmesi yapilmaz;
        // sadece mevcut fiziksel temasi tespit ederiz.
        Vector3 probeCenter = new Vector3(
            ownBounds.center.x,
            ownBounds.min.y + tolerance * 0.5f,
            ownBounds.center.z);

        Vector3 probeHalfExtents = new Vector3(
            Mathf.Max(0.005f, ownBounds.extents.x * 0.92f),
            tolerance * 0.5f,
            Mathf.Max(0.005f, ownBounds.extents.z * 0.92f));

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
            if (otherBook == this)
                continue;

            // Oyuncu fiziksel destek degildir.
            if (candidate.GetComponentInParent<PlayerInteraction>() != null)
                continue;

            Bounds candidateBounds = candidate.bounds;

            // Aday collider'in ustu, bu kitabin altina yakin olmali. Boylece
            // yan yana duran veya kitabin ustunde bulunan collider'lar destek
            // olarak kabul edilmez.
            if (candidateBounds.max.y < ownBounds.min.y - tolerance ||
                candidateBounds.min.y > ownBounds.min.y + tolerance)
                continue;

            // Yatayda gercek bir kesisim/temas olmali.
            if (candidateBounds.max.x < ownBounds.min.x ||
                candidateBounds.min.x > ownBounds.max.x ||
                candidateBounds.max.z < ownBounds.min.z ||
                candidateBounds.min.z > ownBounds.max.z)
                continue;

            if (otherBook != null)
            {
                // Elde tasinan kitap destek sayilmaz. Bu, eldeki kitabin
                // ustundeki kitabin havada donup sabitlenmesini engeller.
                if (otherBook.IsHeld)
                    continue;

                Rigidbody otherRb = otherBook.GetComponent<Rigidbody>();
                if (otherRb != null && otherRb.isKinematic)
                    return true;

                // Alt kitap hala dinamikse onun hareketi bitmeden ustteki
                // kitabi sabitleme.
                continue;
            }

            // Raf/zemin gibi normal collider'lar gecerli destektir.
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
