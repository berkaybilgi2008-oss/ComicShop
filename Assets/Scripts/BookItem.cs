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

    [Header("Destek Kontrolu")]
    [Tooltip("Bir temas yuzeyinin destek sayilmasi icin gereken minimum yukari normal.")]
    [Range(0.1f, 0.9f)] public float supportNormalY = 0.35f;

    private readonly HashSet<BookItem> supportedByBooks = new HashSet<BookItem>();
    private bool supportedByWorld;

    void Awake()
    {
        originalScale = transform.localScale;
        CreateOutlineObjects();
    }

    void FixedUpdate()
    {
        supportedByBooks.Clear();
        supportedByWorld = false;
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision == null || collision.collider == null)
            return;

        if (collision.collider.GetComponentInParent<PlayerInteraction>() != null)
            return;

        BookItem otherBook = collision.collider.GetComponentInParent<BookItem>();
        bool hasUpwardSupport = false;

        ContactPoint[] contacts = collision.contacts;
        for (int i = 0; i < contacts.Length; i++)
        {
            if (contacts[i].normal.y >= supportNormalY)
            {
                hasUpwardSupport = true;
                break;
            }
        }

        if (!hasUpwardSupport)
            return;

        if (otherBook != null && otherBook != this)
            supportedByBooks.Add(otherBook);
        else if (otherBook == null)
            supportedByWorld = true;
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
        return HasPhysicalSupport(new HashSet<BookItem>());
    }

    bool HasPhysicalSupport(HashSet<BookItem> visited)
    {
        if (!visited.Add(this))
            return false;

        if (supportedByWorld)
            return true;

        foreach (BookItem otherBook in supportedByBooks)
        {
            if (otherBook == null || otherBook == this)
                continue;

            Rigidbody otherRb = otherBook.GetComponent<Rigidbody>();
            if (otherRb == null)
                continue;

            // KRITIK: elde tutulan kitap kinematic'tir ama fiziksel temel degildir.
            // Aksi halde havada elde duran kitap, birakilan kitabi havada dondurebilir.
            if (otherBook.IsHeld)
                continue;

            // Kinematic kitap ancak gercekten sabitlenmis bir kitap olabilir.
            if (otherRb.isKinematic || otherRb.IsSleeping() || otherBook.HasPhysicalSupport(visited))
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
            supportedByBooks.Clear();
            supportedByWorld = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = held;
            rb.interpolation = held ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }
    }
}
