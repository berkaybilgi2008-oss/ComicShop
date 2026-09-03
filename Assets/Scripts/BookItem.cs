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
    private readonly HashSet<Collider> supportedByWorldColliders = new HashSet<Collider>();

    void Awake()
    {
        originalScale = transform.localScale;
        CreateOutlineObjects();
    }

    void OnCollisionEnter(Collision collision)
    {
        RegisterSupportCollision(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        RegisterSupportCollision(collision);
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision == null || collision.collider == null)
            return;

        BookItem otherBook = collision.collider.GetComponentInParent<BookItem>();
        if (otherBook != null && otherBook != this)
        {
            supportedByBooks.Remove(otherBook);
            WakeIfUnsupported();
            return;
        }

        if (collision.collider.GetComponentInParent<PlayerInteraction>() == null)
        {
            supportedByWorldColliders.Remove(collision.collider);
            WakeIfUnsupported();
        }
    }

    void RegisterSupportCollision(Collision collision)
    {
        if (collision == null || collision.collider == null)
            return;

        if (collision.collider.GetComponentInParent<PlayerInteraction>() != null)
            return;

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

        BookItem otherBook = collision.collider.GetComponentInParent<BookItem>();
        if (otherBook != null && otherBook != this)
            supportedByBooks.Add(otherBook);
        else
            supportedByWorldColliders.Add(collision.collider);
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

        if (HasValidWorldSupport())
            return true;

        foreach (BookItem otherBook in supportedByBooks)
        {
            if (otherBook == null || otherBook == this || otherBook.IsHeld)
                continue;

            Rigidbody otherRb = otherBook.GetComponent<Rigidbody>();
            if (otherRb == null)
                continue;

            if (otherRb.isKinematic || otherRb.IsSleeping())
            {
                if (otherBook.HasPhysicalSupport(visited))
                    return true;
            }
        }

        return false;
    }

    bool HasValidWorldSupport()
    {
        foreach (Collider collider in supportedByWorldColliders)
        {
            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    void WakeIfUnsupported()
    {
        if (IsHeld)
            return;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null || !rb.isKinematic)
            return;

        if (HasPhysicalSupport())
            return;

        rb.isKinematic = false;
        rb.WakeUp();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        stillTimer = 0f;
    }

    void WakeBooksDependingOnThis()
    {
        BookItem[] allBooks = FindObjectsByType<BookItem>(FindObjectsSortMode.None);
        HashSet<BookItem> visited = new HashSet<BookItem>();
        WakeDependentsRecursive(allBooks, visited);
    }

    void WakeDependentsRecursive(BookItem[] allBooks, HashSet<BookItem> visited)
    {
        if (!visited.Add(this))
            return;

        foreach (BookItem otherBook in allBooks)
        {
            if (otherBook == null || otherBook == this || !otherBook.supportedByBooks.Contains(this))
                continue;

            otherBook.supportedByBooks.Remove(this);

            if (otherBook.IsHeld)
                continue;

            Rigidbody otherRb = otherBook.GetComponent<Rigidbody>();
            if (otherRb == null)
                continue;

            if (!otherBook.HasPhysicalSupport())
            {
                otherRb.isKinematic = false;
                otherRb.WakeUp();
                otherBook.stillTimer = 0f;
            }

            otherBook.WakeDependentsRecursive(allBooks, visited);
        }
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
        if (held && !IsHeld)
            WakeBooksDependingOnThis();

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
            supportedByWorldColliders.Clear();
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = held;
            rb.interpolation = held ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }
    }
}
