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
            return;
        }

        if (collision.collider.GetComponentInParent<PlayerInteraction>() == null)
            RecheckWorldSupport();
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
            supportedByWorld = true;
    }

    void RecheckWorldSupport()
    {
        supportedByWorld = false;

        Collider[] ownColliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider ownCollider in ownColliders)
        {
            if (ownCollider == null || !ownCollider.enabled)
                continue;

            Collider[] nearby = Physics.OverlapBox(
                ownCollider.bounds.center,
                ownCollider.bounds.extents + Vector3.one * 0.02f,
                ownCollider.transform.rotation,
                ~0,
                QueryTriggerInteraction.Ignore);

            foreach (Collider otherCollider in nearby)
            {
                if (otherCollider == null || otherCollider == ownCollider)
                    continue;

                if (otherCollider.transform.IsChildOf(transform))
                    continue;

                if (otherCollider.GetComponentInParent<PlayerInteraction>() != null)
                    continue;

                Vector3 closestPoint = otherCollider.ClosestPoint(ownCollider.bounds.center);
                Vector3 directionFromOther = transform.position - closestPoint;
                if (directionFromOther.y <= 0f)
                    continue;

                supportedByWorld = true;
                return;
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

            // Elde duran kinematic kitap fiziksel destek degildir.
            if (otherBook.IsHeld)
                continue;

            // Kinematic veya sleeping kitap ancak kendisi destek zincirine
            // bagliysa ustundeki kitabi destekleyebilir.
            if (otherRb.isKinematic || otherRb.IsSleeping())
            {
                if (otherBook.HasPhysicalSupport(visited))
                    return true;
            }
            else if (otherBook.HasPhysicalSupport(visited))
            {
                return true;
            }
        }

        return false;
    }

    void WakeBooksDependingOnThis()
    {
        BookItem[] allBooks = FindObjectsByType<BookItem>(FindObjectsSortMode.None);

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
            supportedByWorld = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = held;
            rb.interpolation = held ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }
    }
}
