using UnityEngine;

// BU SCRIPT, GERCEK BIR KITAPLIK (bookcase) MODELINE eklenir -- her fiziksel
// kitaplik kendi BookcaseSlotGenerator'ina sahip olur. Boylece raflar, o
// kitapligin KENDI konumuna/boyutuna gore olusturulur, uzak/soyut bir grid'e
// degil.
//
// KULLANIM (arkadasin icin):
// 1) Gercek kitaplik modelini sahneye yerlestir.
// 2) Uzerine bu script'i ekle (Add Component > Bookcase Slot Generator).
// 3) "Slot Prefab" alanina raf prefabini surukle.
// 4) "Brand ID" alanina bu kitapligin hangi markaya ait oldugunu yaz
//    (1. marka icin 0, 2. marka icin 1, ... BrandConfig.cs'teki siraya gore).
// 5) Kucuk markalar (5 kahraman, TEK kitaplik) icin:
//       Local Hero Start = 0, Local Hero Count = 5
//    Buyuk markalar (10 kahraman, IKI kitapliga bolunmus) icin:
//       Ilk kitaplik:  Local Hero Start = 0, Local Hero Count = 5
//       Ikinci kitaplik: Local Hero Start = 5, Local Hero Count = 5
// 6) Volume Column Spacing / Hero Row Spacing / Start Offset degerlerini,
//    Scene view'da rafların gercek kitaplik gozlerine oturana kadar dene-yanil
//    yontemiyle ayarla.
// 7) Component basligina sag tik (ya da ⋮ simgesi) -> "Bu Kitapligin
//    Raflarini Olustur" de. Raflar bu objenin CHILD'i olarak, onun
//    pozisyonuna GORE olusur -- kitapligi tasirsan raflar da onunla gelir.
public class BookcaseSlotGenerator : MonoBehaviour
{
    [Header("Slot (Raf) Prefab")]
    [Tooltip("Icinde ShelfSlot script'i ve Collider olan raf prefabi")]
    public GameObject slotPrefab;

    [Header("Bu Kitaplik Hangi Markayi Tasiyor")]
    [Tooltip("BrandConfig.cs'teki marka sirasi (0 = 1. marka, 1 = 2. marka, ...)")]
    public int brandID;

    [Tooltip("Bu markanin kahramanlarindan HANGI ARALIGI bu kitaplikta olacak. " +
        "5 kahramanli kucuk bir marka TEK kitaplikta tamamen sigar (Start=0, Count=5). " +
        "10 kahramanli buyuk bir marka IKI kitapliga bolunur (1. kitaplik Start=0 Count=5, " +
        "2. kitaplik Start=5 Count=5).")]
    public int localHeroStart = 0;
    public int localHeroCount = 5;

    [Header("Cilt/Kopya Ayarlari (butun kitapliklarda ayni kalir)")]
    public int volumesPerHero = 3;
    public int copiesPerVolume = 10;

    [Header("Bu Kitapligin Kendi Grid Ayarlari (GERCEK modele gore ayarlanacak)")]
    [Tooltip("Ayni kahramanin cilt sutunlari arasi mesafe (bu kitapligin KENDI local X ekseninde)")]
    public float volumeColumnSpacing = 1.2f;
    [Tooltip("Farkli kahramanlarin satirlari arasi mesafe (raf kati farki gibi dusun, local Y)")]
    public float heroRowSpacing = 0.5f;
    [Tooltip("Ilk rafin bu objeye (kitapligin pivot noktasina) gore baslangic konumu")]
    public Vector3 startOffset = Vector3.zero;

    [ContextMenu("Bu Kitapligin Raflarini Olustur")]
    public void GenerateSlots()
    {
        if (slotPrefab == null)
        {
            Debug.LogError($"BookcaseSlotGenerator ({name}): Slot Prefab atanmamis.");
            return;
        }

        int heroStartGlobal = BrandConfig.GetHeroRangeStart(brandID) + localHeroStart;
        int totalCreated = 0;

        for (int i = 0; i < localHeroCount; i++)
        {
            int globalHeroID = heroStartGlobal + i;
            float rowY = startOffset.y - i * heroRowSpacing;

            for (int vol = 0; vol < volumesPerHero; vol++)
            {
                float colX = startOffset.x + vol * volumeColumnSpacing;
                Vector3 localPos = new Vector3(colX, rowY, startOffset.z);

                GameObject slotObj = Instantiate(slotPrefab, transform);
                slotObj.transform.localPosition = localPos;
                slotObj.transform.localRotation = Quaternion.identity;
                slotObj.name = $"Slot_Marka{brandID + 1}_Hero{globalHeroID + 1}_Cilt{vol + 1}";

                ShelfSlot slot = slotObj.GetComponent<ShelfSlot>();
                if (slot != null)
                {
                    slot.brandID = brandID;
                    slot.capacity = copiesPerVolume;
                }

                totalCreated++;
            }
        }

        Debug.Log($"BookcaseSlotGenerator ({name}): {totalCreated} raf olusturuldu "
            + $"(Marka {brandID + 1}, Hero {heroStartGlobal + 1}-{heroStartGlobal + localHeroCount}).");
    }

    [ContextMenu("Bu Kitapligin Raflarini Temizle")]
    public void ClearSlots()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
#if UNITY_EDITOR
            DestroyImmediate(child);
#else
            Destroy(child);
#endif
        }
        Debug.Log($"BookcaseSlotGenerator ({name}): raflar temizlendi.");
    }
}