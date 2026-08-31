using UnityEngine;

// Bu raf (slot) ARTIK onceden hangi kahraman/cilde ait oldugu belirlenmis
// degil. Sadece hangi MARKAYA (brandID) ait oldugu sabit -- fiziksel olarak
// hangi kitapligin altinda durdugunu belirler. Ilk kitap konuldugunda, raf o
// kitabin (hero+cilt) kimligini "kapiyor" ve sadece O kimlikten kopyalari
// kabul ediyor (10'a kadar). Raf tamamen bosalinca kimligini kaybedip
// tekrar "bos, ayni markadan herhangi bir kitabi kabul edebilir" haline donuyor.
public class ShelfSlot : MonoBehaviour
{
    [Header("Marka (SABIT -- ShelfSlotGenerator tarafindan atanir)")]
    [Tooltip("Bu raf hangi markanin kitapligi altinda duruyor. Sadece bu markadan kitaplar buraya konabilir.")]
    public int brandID;

    [Header("Kapasite")]
    [Tooltip("Bu rafa (bir kere bir kimlik 'kapinca') en fazla kac KOPYA sigar")]
    public int capacity = 10;
    private int filledCount = 0;

    // -1 = raf bos, henuz kimse buraya kitap koymadi (herhangi bir kahraman/cilt olabilir)
    private int claimedHeroID = -1;
    private int claimedVolumeID = -1;

    [Header("Yerlesim Ayari")]
    [Tooltip("Kitabin varsayilan durusu ile bu slotun istedigi durus arasindaki fark. Kitap yan/ters oturuyorsa buradan eksen bazinda 90 derece dene.")]
    public Vector3 placementRotationOffset = Vector3.zero;
    [Tooltip("Ayni slota konan her ek kopyanin, bir oncekine gore ne kadar kaydirilacagi (yan yana dizilsin diye)")]
    public float copyStackOffset = 0.06f;

    public bool IsAvailable => filledCount < capacity;
    public bool IsClaimed => claimedHeroID != -1;

    // Elimizdeki kitap bu rafa konabilir mi?
    // 1) Marka uyusmali (X markasinin kitabi sadece X markasinin raflarina girer)
    // 2) Raf bossa (kimse kapmamissa) HERHANGI bir hero+cilt kabul edilir
    // 3) Raf doluysa (kapilmissa) SADECE ayni hero+cilt kabul edilir
    public bool Matches(BookItem book)
    {
        if (book.brandID != brandID) return false;
        if (!IsClaimed) return true;
        return book.heroID == claimedHeroID && book.volumeID == claimedVolumeID;
    }

    public bool PlaceBook(BookItem book)
    {
        if (!Matches(book) || !IsAvailable) return false;

        // Raf bossa, bu kitabin kimligini simdi "kap"
        if (!IsClaimed)
        {
            claimedHeroID = book.heroID;
            claimedVolumeID = book.volumeID;
        }

        // Ayni slota konan her kopya, KENDI SABIT numarasina (copyIndex, 0-9) gore
        // bir yuvaya oturur -- hangi sirada getirilirse getirilsin, 9 numarali kopya
        // HER ZAMAN 9. yuvaya gider. filledCount SADECE kapasite/sayim icin kullanilir,
        // pozisyon icin degil.
        // ONEMLI: Kayma yonunu artik kitabin rotasyonundan (finalRotation) DEGIL,
        // dogrudan RAFIN kendi sabit yonunden (transform.right) hesapliyoruz.
        // Boylece kitabin durus rotasyonunu (placementRotationOffset) her degistirdiginde
        // kayma yonu ETKILENMEZ -- ikisi artik birbirinden tamamen bagimsiz.
        Quaternion finalRotation = transform.rotation * Quaternion.Euler(placementRotationOffset);
        Vector3 widthDirection = transform.right;
        Vector3 offset = widthDirection * (book.copyIndex * copyStackOffset);

        book.transform.SetParent(null);
        book.transform.position = transform.position + offset;
        book.transform.rotation = finalRotation;
        book.transform.localScale = book.OriginalScale; // rafta her zaman gercek boyutunda otursun
        book.SetHeld(false);
        book.GetComponent<Collider>().enabled = true;

        Rigidbody rb = book.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        filledCount++;
        book.currentSlot = this;
        GameStats.RegisterPlacement(book.heroID);
        return true;
    }

    // Oyuncu bu raftaki bir kitabi geri eline aldiginda cagirilir.
    // Raf tamamen bosalirsa (filledCount 0'a duserse) kimligini KAYBEDER,
    // tekrar herhangi bir kahraman/cildi kabul edebilir hale gelir.
    public void RemoveBook(BookItem book)
    {
        if (filledCount > 0) filledCount--;
        GameStats.UnregisterPlacement(book.heroID);
        book.currentSlot = null;

        if (filledCount == 0)
        {
            claimedHeroID = -1;
            claimedVolumeID = -1;
        }
    }
}