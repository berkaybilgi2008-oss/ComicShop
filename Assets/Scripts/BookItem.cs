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

    [Header("Destek Kontrolu")]
    [Tooltip("Kitabin altinda destek kabul etmek icin kullanilan yerel ray yuksekligi.")]
    public float supportRayHeight = 0.03f;
    [Tooltip("Destek yuzeyi ile kitap arasinda kabul edilen maksimum mesafe.")]
    public float supportRayDistance = 0.08f;
    [Tooltip("Destek raylarinin kitap genisligine gore yatay yaricapi.")]
    [Range(0.1f, 0.9f)] public float supportProbeHalfWidth = 0.75f;

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

        Bounds bounds = GetCombinedColliderBounds();
        Vector3 center = bounds.center;
        float bottomY = bounds.min.y;
        float halfX = Mathf.Max(0.01f, bounds.extents.x * supportProbeHalfWidth);
        float halfZ = Mathf.Max(0.01f, bounds.extents.z * supportProbeHalfWidth);
        float startY = bottomY + supportRayHeight;
        float distance = Mathf.Max(supportRayDistance, supportRayHeight + 0.02f);

        Vector3[] origins =
        {
            new Vector3(center.x, startY, center.z),
            new Vector3(center.x - halfX, startY, center.z - halfZ),
            new Vector3(center.x - halfX, startY, center.z + halfZ),
            new Vector3(center.x + halfX, startY, center.z - halfZ),
            new Vector3(center.x + halfX, startY, center.z + halfZ)
        };

        int supportHits = 0;

        foreach (Vector3 origin in origins)
        {
            // Tum kitaplar ayni layer'da oldugu icin layer mask kullanmiyoruz.
            // Onceki yaklasim kitap layer'ini komple maskeleyip kitap-ustune-kitap
            // destegini fiziksel olarak gormeyi imkansiz hale getiriyordu.
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool originSupported = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.normal.y < 0.2f)
                    continue;

                BookItem otherBook = hit.collider.GetComponentInParent<BookItem>();
                if (otherBook == this)
                    continue;

                // Oyuncu ile kitap arasindaki fiziksel ignore durumunu raycast ile
                // yanlislikla destek kabul etmiyoruz.
                if (hit.collider.GetComponentInParent<PlayerInteraction>() != null)
                    continue;

                if (otherBook != null)
                {
                    Rigidbody otherRb = otherBook.GetComponent<Rigidbody>();
                    if (otherRb == null)
                        continue;

                    // Alt kitap zaten sabitse dogrudan fiziksel temel kabul edilir.
                    // Dinamik ama yavaslamis bir alt kitap icin de zinciri takip et:
                    // onun altinda gercek bir zemin/raf veya sabit kitap varsa destek
                    // gercekten fiziksel bir zincire dayanir.
                    if (otherRb.isKinematic || otherRb.IsSleeping() ||
                        otherBook.HasPhysicalSupport(visited))
                    {
                        originSupported = true;
                        break;
                    }

                    // Ilk dinamik kitap destek degilse onun arkasindaki nesneyi
                    // ayni ray uzerinde aramaya devam ediyoruz.
                    continue;
                }

                // Kitap olmayan collider, yukaridan gelen bir temas yuzeyi ise
                // zemin/raf gibi fiziksel destek sayilir.
                originSupported = true;
                break;
            }

            if (originSupported)
                supportHits++;
        }

        return supportHits >= 2;
    }

    Bounds GetCombinedColliderBounds()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        bool hasBounds = false;
        Bounds combined = new Bounds(transform.position, Vector3.zero);

        foreach (Collider col in colliders)
        {
            if (col == null || !col.enabled)
                continue;

            if (!hasBounds)
            {
                combined = col.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(col.bounds);
            }
        }

        return hasBounds ? combined : new Bounds(transform.position, Vector3.zero);
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
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = held;
            rb.interpolation = held ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }
    }
}
