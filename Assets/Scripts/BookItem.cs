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

    // Ayni hero+cilt kombinasyonuna sahip TUM kopyalar (10 tanesi) bu ID'yi paylasir.
    // Slot eslestirmesi bununla yapiliyor.
    public int TypeID => heroID * 1000 + volumeID; // 1000 carpani, ID'lerin karismamasi icin guvenli bir bosluk birakiyor

    // UI'da gosterilecek okunabilir isim, orn: "Orbital Rebel - Cilt 2"
    public string DisplayName => $"{heroName} - Cilt {volumeID + 1}";

    [Header("Kenar (Outline) Highlight Ayarlari")]
    [Tooltip("Sadece kenarlarin parlamasini saglayan materyal. Parlak renkli, Unlit bir materyal olmali.")]
    public Material outlineMaterial;
    [Tooltip("Kenar payinin ne kadar kalin gorunecegi. 1.0 = kitapla ayni boy (gorunmez), 1.05-1.1 arasi iyi bir baslangic.")]
    public float outlineScale = 1.05f;

    [Header("Kapak Gorseli (on yuzdeki ayri Quad)")]
    [Tooltip("Kitabin ustune yapistirdigimiz ince kapak yuzeyinin (CoverQuad) Renderer'i. Bunu Inspector'dan CoverQuad objesinden surukle.")]
    public Renderer coverRenderer;

    private GameObject outlineObject;
    private Vector3 originalScale;

    // Bu kitap su an bir rafta duruyorsa, hangi ShelfSlot'ta oldugunu tutar.
    // Yerdeyken veya elimizdeyken null'dur. Geri alma (E ile tekrar elimize
    // alma) yapabilmek icin hangi slotu bosaltmamiz gerektigini bilmemiz lazim.
    public ShelfSlot currentSlot;

    public bool IsHeld { get; private set; }
    public Vector3 OriginalScale => originalScale;

    void Awake()
    {
        originalScale = transform.localScale; // yerdeki/raftaki gercek boyutunu hatirla
        CreateOutlineObject();
    }

    // BookSpawner bunu cagirarak bu kitaba rastgele bir kapak resmi atar.
    // Kitabin govdesine (yanlarina) DOKUNMAZ, sadece ustteki ince CoverQuad'i degistirir.
    public void SetCoverMaterial(Material coverMaterial)
    {
        if (coverRenderer != null && coverMaterial != null)
            coverRenderer.material = coverMaterial;
    }

    // Kitabin mesh'inin biraz buyutulmus, kendi materyaliyle ayri bir kopyasini olusturur.
    // Orijinal mesh ustune bindigi icin sadece kenarlardan tasan kisim gorunur -> "outline" efekti.
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

        // Baslangicta kapali, sadece bakinca acilacak
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
        GetComponent<Collider>().enabled = !held; // elimizdeyken carpisma kapansin
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = held; // elimizdeyken fizik uygulanmasin
    }
}