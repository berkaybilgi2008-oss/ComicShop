using UnityEngine;

public class BookItem : MonoBehaviour
{
    [Header("Kimlik (Super Kahraman Sistemi)")]
    [Tooltip("Bu kitap hangi super kahramana ait (0-119 arasi, 120 kahraman icin)")]
    public int heroID;
    [Tooltip("Bu kitap hangi markaya ait (BookSpawner tarafindan heroID'den otomatik hesaplanir)")]
    public int brandID;
    [Tooltip("Bu kitap kahramanin kacinci cildi (0, 1 veya 2 -- 3 cilt icin)")]
    public int volumeID;
    [Tooltip("Kahramanin gercek ismi (BookSpawner tarafindan otomatik atanir, UI'da gosterilir)")]
    public string heroName = "Bilinmeyen Kahraman";

    [Tooltip("Bu kitap, ayni hero+cilt icindeki 10 kopyadan KACINCISI (0-9). Rafta HER ZAMAN kendi sabit yuvasina oturur, hangi sirada getirilirse getirilsin.")]
    public int copyIndex;

    public int TypeID => heroID * 1000 + volumeID;
    public string DisplayName => $"{heroName} - Cilt {volumeID + 1}";

    [Header("Kenar (Outline) Highlight Ayarlari")]
    public Material outlineMaterial;
    public float outlineScale = 1.05f;

    [Header("Kapak Gorseli (on yuzdeki ayri Quad)")]
    public Renderer coverRenderer;

    private GameObject outlineObject;
    private Vector3 originalScale;

    public ShelfSlot currentSlot;

    public bool IsHeld { get; private set; }
    public Vector3 OriginalScale => originalScale;

    [Header("Birakma Fizigi")]
    [Tooltip("Kitabin fiziksel hareketi bu hizlarin altina dustugunde uykuya alinir.")]
    public float sleepLinearVelocity = 0.03f;
    public float sleepAngularVelocity = 0.03f;
    [Tooltip("Kitap bu kadar sure boyunca dusuk hizda kalirsa fizik kapanir.")]
    public float sleepDelay = 0.25f;

    private float stillTimer;

    void Awake()
    {
        originalScale = transform.localScale;
        CreateOutlineObject();
    }

    void Update()
    {
        if (IsHeld)
            return;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null || rb.isKinematic)
            return;

        // Kitap gercekten hareket etmeyi bitirene kadar fizik acik kalir.
        // Anlik bir carpismada hemen kapatmak yerine kisa bir sure boyunca
        // hem linear hem angular hizlarin cok dusuk olmasini bekliyoruz.
        if (rb.linearVelocity.sqrMagnitude <= sleepLinearVelocity * sleepLinearVelocity &&
            rb.angularVelocity.sqrMagnitude <= sleepAngularVelocity * sleepAngularVelocity)
        {
            stillTimer += Time.deltaTime;

            if (stillTimer >= sleepDelay)
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

    public void SetCoverMaterial(Material coverMaterial)
    {
        if (coverRenderer != null && coverMaterial != null)
            coverRenderer.material = coverMaterial;
    }

    void CreateOutlineObject()
    {
        MeshFilter sourceMeshFilter = GetComponentInChildren<MeshFilter>();
        if (sourceMeshFilter == null || outlineMaterial == null)
        {
            Debug.LogWarning("BookItem: Outline olusturulamadi, MeshFilter veya Outline Material eksik. (" + gameObject.name + ")");
            return;
        }

        outlineObject = new GameObject("Outline");
        outlineObject.transform.SetParent(sourceMeshFilter.transform, false);
        outlineObject.transform.localPosition = Vector3.zero;
        outlineObject.transform.localRotation = Quaternion.identity;
        outlineObject.transform.localScale = Vector3.one * outlineScale;

        MeshFilter mf = outlineObject.AddComponent<MeshFilter>();
        mf.mesh = sourceMeshFilter.sharedMesh;

        MeshRenderer mr = outlineObject.AddComponent<MeshRenderer>();
        mr.material = outlineMaterial;

        outlineObject.SetActive(false);
    }

    public void SetHighlight(bool on)
    {
        if (outlineObject != null)
            outlineObject.SetActive(on);
    }

    public void SetHeld(bool held)
    {
        IsHeld = held;
        GetComponent<Collider>().enabled = !held;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            stillTimer = 0f;
            rb.isKinematic = held;
        }
    }
}
