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

    // Eski alan geriye uyumluluk icin tutuluyor.
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

    // Kitap havada yavasladiginda sabitlenmemeli. Sabitlenmesi icin
    // gercekten zemine/rafa veya desteklenen baska bir kitaba temas etmesi gerekir.
    private bool directSupportContact;
    private readonly HashSet<BookItem> supportedBookContacts = new HashSet<BookItem>();

    void Awake()
    {
        originalScale = transform.localScale;
        CreateOutlineObjects();
    }

    void FixedUpdate()
    {
        // Collision callback'leri bu physics adiminda yeniden doldurur.
        directSupportContact = false;
        supportedBookContacts.Clear();
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

            // Dusme hareketi bitmis olsa bile havadaysa sabitleme yapma.
            // Baska bir kitabin ustundeyse o kitabin da gercekten destekleniyor
            // olmasini kontrol ediyoruz; boylece havadaki kitap zinciri donup kalmaz.
            if (stillTimer >= sleepDelay && IsPhysicallySupported())
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                stillTimer = 0f;
            }
        }
        else
        {
            stillTimer = 0f;
        }
    }

    bool IsPhysicallySupported()
    {
        return IsPhysicallySupported(new HashSet<BookItem>());
    }

    bool IsPhysicallySupported(HashSet<BookItem> visited)
    {
        if (!visited.Add(this))
            return false;

        if (directSupportContact)
            return true;

        foreach (BookItem otherBook in supportedBookContacts)
        {
            if (otherBook != null && otherBook.IsPhysicallySupported(visited))
                return true;
        }

        return false;
    }

    void OnCollisionStay(Collision collision)
    {
        if (IsHeld || collision == null)
            return;

        BookItem otherBook = collision.collider.GetComponentInParent<BookItem>();
        bool hasUpwardSupport = false;

        ContactPoint[] contacts = collision.contacts;
        for (int i = 0; i < contacts.Length; i++)
        {
            // Normal, diger yuzeyden bu kitaba dogru oldugu icin yukari bakan
            // bir normal, kitabin altindan destek aldigini gosterir.
            if (contacts[i].normal.y > 0.2f)
            {
                hasUpwardSupport = true;
                break;
            }
        }

        if (!hasUpwardSupport)
            return;

        if (otherBook != null && otherBook != this)
            supportedBookContacts.Add(otherBook);
        else
            directSupportContact = true;
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
        // Boylece eldeki kitaplar da birbirleriyle fiziksel olarak etkilesir.
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
            directSupportContact = false;
            supportedBookContacts.Clear();
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = held;
            rb.interpolation = held ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }
    }
}
