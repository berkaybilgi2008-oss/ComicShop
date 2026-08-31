using UnityEngine;

// Bu script, BrandConfig'teki marka/kahraman yapisina gore raflari OTOMATIK
// olusturur. Play moduna GEREK YOK -- Inspector'da component basligina sag
// tik yapip "Slotlari Olustur" diyerek Editor'de calistirabilirsin.
//
// ONEMLI: Artik her KOPYA icin ayri bir raf degil, her (hero, cilt) EN AZ bir
// raf paylasabiliyor (rafin kapasitesi 10 oldugu icin). O yuzden toplam raf
// sayisi = TotalHeroCount * volumesPerHero (120 * 3 = 360), 3600 degil.
public class ShelfSlotGenerator : MonoBehaviour
{
    [Header("Slot (Raf) Prefab")]
    [Tooltip("Icinde ShelfSlot script'i ve Collider olan, elle hazirladigin bir raf prefabi")]
    public GameObject slotPrefab;

    [Header("Kimlik Sistemi")]
    [Tooltip("Her kahramanin kac cildi var (sabit: 3) -- BrandConfig'teki kahraman sayisiyla carpilir")]
    public int volumesPerHero = 3;
    [Tooltip("Her rafin tasiyabilecegi maksimum kopya sayisi (sabit: 10)")]
    public int copiesPerVolume = 10;

    [Header("Yerlesim Ayarlari (kaba grid -- sonra gercek rafa gore ayarlanacak)")]
    [Tooltip("Bir markanin icindeki cilt sutunlari arasi mesafe (X) -- 10 kopya yan yana sigacak kadar genis olmali (capacity x copyStackOffset'ten BUYUK olsun)")]
    public float volumeColumnSpacing = 0.8f;
    [Tooltip("Bir markanin icindeki kahraman satirlari arasi mesafe (Y) -- raf kati gibi dusun")]
    public float heroRowSpacing = 0.25f;
    [Tooltip("Bir markadan digerine geciste ne kadar yana (X) kayilacagi (bir 'kitaplik' genisligi gibi dusun)")]
    public float brandSpacingX = 1.5f;
    [Tooltip("Bir 'satirda' kac marka olsun, bu sayidan sonra bir sonraki satira (Z ekseninde) gecilir")]
    public int brandsPerRow = 5;
    [Tooltip("Bir marka satirindan digerine geciste ne kadar ileri (Z) kayilacagi")]
    public float brandRowSpacingZ = 2f;

    [ContextMenu("Slotlari Olustur")]
    public void GenerateSlots()
    {
        if (slotPrefab == null)
        {
            Debug.LogError("ShelfSlotGenerator: Slot Prefab atanmamis, once onu doldur.");
            return;
        }

        int totalCreated = 0;

        for (int brand = 0; brand < BrandConfig.BrandCount; brand++)
        {
            int heroesInBrand = BrandConfig.heroesPerBrand[brand];
            int heroStart = BrandConfig.GetHeroRangeStart(brand);

            int rowIndex = brand / brandsPerRow;
            int colIndex = brand % brandsPerRow;
            float brandBaseX = colIndex * brandSpacingX;
            float brandBaseZ = rowIndex * brandRowSpacingZ;

            // Bu markanin her kahramani bir "satir", her cildi bir "sutun"
            for (int localHero = 0; localHero < heroesInBrand; localHero++)
            {
                float rowY = -localHero * heroRowSpacing; // asagi dogru sıralansin

                for (int vol = 0; vol < volumesPerHero; vol++)
                {
                    float colX = brandBaseX + vol * volumeColumnSpacing;

                    Vector3 localPos = new Vector3(colX, rowY, brandBaseZ);

                    GameObject slotObj = Instantiate(slotPrefab, transform);
                    slotObj.transform.localPosition = localPos;
                    slotObj.transform.localRotation = Quaternion.identity;
                    slotObj.name = $"Slot_Marka{brand + 1}_Hero{heroStart + localHero + 1}_Cilt{vol + 1}";

                    ShelfSlot slot = slotObj.GetComponent<ShelfSlot>();
                    if (slot != null)
                    {
                        slot.brandID = brand; // SADECE marka sabit, hero/cilt artik yok -- dinamik kapiliyor
                        slot.capacity = copiesPerVolume;
                    }

                    totalCreated++;
                }
            }
        }

        Debug.Log($"ShelfSlotGenerator: {totalCreated} raf olusturuldu "
            + $"({BrandConfig.BrandCount} marka, toplam {BrandConfig.TotalHeroCount} kahraman x {volumesPerHero} cilt).");
    }

    [ContextMenu("Olusturulan Slotlari Temizle")]
    public void ClearGeneratedSlots()
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
        Debug.Log("ShelfSlotGenerator: tum olusturulan raflar silindi.");
    }
}