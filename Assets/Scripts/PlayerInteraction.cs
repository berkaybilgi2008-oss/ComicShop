using System.Collections.Generic;
using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Referanslar")]
    public Camera playerCamera;

    [Tooltip("Tum kitaplarin (1'den 5'e kadar) uzerine biriktigi tek nokta")]
    public Transform rightHandPoint;

    [Header("Tasima Ayarlari")]
    [Tooltip("Ayni anda en fazla kac kitap tasiyabiliriz")]
    public int maxHeldBooks = 10;

    [Tooltip("Yigindaki kitaplar arasindaki dikey bosluk (kitap kalinligina yakin bir deger sec)")]
    public float stackSpacing = 0.06f;

    [Tooltip("Kitaplar elimizdeyken orijinal boyutunun yuzde kaci gosterilsin (0.5 = yarisi kadar kucuk, 1.0 = normal boyut)")]
    [Range(0.2f, 1f)]
    public float heldScaleMultiplier = 0.55f;

    [Tooltip("Elimizdeyken SADECE kalinligin (Y ekseni) ek olarak ne kadar incelecegi. 0.8 = kalinlik %20 daha ince, genislik/uzunluk etkilenmez.")]
    [Range(0.2f, 1f)]
    public float heldThicknessMultiplier = 0.8f;

    [Header("Ayarlar")]
    public float interactRange = 3f;
    public LayerMask interactMask; // Inspector'dan Book + Shelf layer'larini sec

    private List<BookItem> heldBooks = new List<BookItem>();

    // HUD ve diger script'lerin okuyabilmesi icin disariya salt-okunur erisim
    public IReadOnlyList<BookItem> HeldBooksList => heldBooks;
    public int MaxHeldBooks => maxHeldBooks;
    private BookItem lookedBook;      // su an baktigimiz yerdeki kitap
    private ShelfSlot lookedSlot;     // su an baktigimiz raf slotu

    void Update()
    {
        HandleLookDetection();

        if (Input.GetKeyDown(KeyCode.E))
            HandleInteractPress();
    }

    void HandleLookDetection()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)); // ekran ortasi

        // Onceki frame'in highlight'ini temizle
        if (lookedBook != null) { lookedBook.SetHighlight(false); lookedBook = null; }
        lookedSlot = null; // artik ghost yok, sadece "bir slota bakiyor muyuz" bilgisi yeterli

        // RaycastAll kullanip mesafeye gore siraliyoruz -- boylece raftaki bir
        // kitabin ustunde durdugu ShelfSlot'un collider'i araya girip kitabi
        // "gizlemesin". Kitaba HER ZAMAN slot'tan once oncelik veriyoruz.
        RaycastHit[] hits = Physics.RaycastAll(ray, interactRange, interactMask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool canPickMore = heldBooks.Count < maxHeldBooks;
        BookItem foundBook = null;
        ShelfSlot foundSlot = null;

        // 1) Once, en yakindan baslayarak alinabilecek bir kitap var mi diye bak
        // (yerde duran ya da RAFTA duran fark etmez -- ikisi de IsHeld=false)
        foreach (RaycastHit hit in hits)
        {
            BookItem book = hit.collider.GetComponent<BookItem>();
            if (book != null && canPickMore && !book.IsHeld)
            {
                foundBook = book;
                break;
            }
        }

        // 2) Kitap bulunamadiysa, bir rafa/slota bakip bakmadigimizi kontrol et
        // (artik hangi slot oldugu onemli degil, sadece "rafa bakiyoruz" bilgisi
        // yeterli -- gercek eslestirme E'ye basinca TUM raflarda taranacak)
        if (foundBook == null)
        {
            foreach (RaycastHit hit in hits)
            {
                ShelfSlot slot = hit.collider.GetComponent<ShelfSlot>();
                if (slot != null && heldBooks.Count > 0)
                {
                    foundSlot = slot;
                    break;
                }
            }
        }

        if (foundBook != null)
        {
            lookedBook = foundBook;
            lookedBook.SetHighlight(true);
        }
        else if (foundSlot != null)
        {
            lookedSlot = foundSlot;
        }
    }

    void HandleInteractPress()
    {
        // Durum 1: Yerde bir kitaba bakiyoruz ve elimizde hala yer var -> al
        if (lookedBook != null && heldBooks.Count < maxHeldBooks)
        {
            PickUp(lookedBook);
            return;
        }

        // Durum 2: Elimizde kitap var, bir rafa/slota bakiyoruz -> elimizdeki
        // TUM kitaplari, dogru olan slotlarini SAHNEDEKI TUM raflarda arayip
        // otomatik yerlestir (tek tek nisan almaya gerek yok)
        if (heldBooks.Count > 0 && lookedSlot != null)
        {
            AutoPlaceAllMatchingBooks();
            return;
        }

        // Durum 3: Ne yerde bir kitaba ne de bos bir slota bakiyoruz, ama elimizde
        // kitap var -> E'ye basinca en usttekini yere birak
        if (heldBooks.Count > 0 && lookedBook == null && lookedSlot == null)
        {
            DropTopBook();
        }
    }

    // Elimizdeki TUM kitaplari kontrol eder, her biri icin sahnedeki TUM
    // ShelfSlot'lari tarayip dogru (esleyen) ve BOS bir slot bulursa oraya
    // otomatik yerlestirir. Uygun slotu olmayan kitaplar elimizde kalir,
    // hicbir uyari/gosterge verilmez (istenen davranis bu).
    void AutoPlaceAllMatchingBooks()
    {
        ShelfSlot[] allSlots = FindObjectsOfType<ShelfSlot>();

        // Sondan basa dogru geziyoruz ki listeden eleman cikardikca
        // index kaymasi sorun yaratmasin
        for (int i = heldBooks.Count - 1; i >= 0; i--)
        {
            BookItem book = heldBooks[i];
            ShelfSlot matchingSlot = FindAvailableSlotFor(book, allSlots);

            if (matchingSlot != null)
            {
                bool placed = matchingSlot.PlaceBook(book);
                if (placed)
                {
                    heldBooks.RemoveAt(i);
                }
            }
        }

        RepositionHeldBooks(); // kalan kitaplari (varsa) dogru yerlere tekrar diz
    }

    // Verilen kitaba uyan (hero+cilt eslesen) ve hala yer olan ilk slotu bulur.
    ShelfSlot FindAvailableSlotFor(BookItem book, ShelfSlot[] allSlots)
    {
        foreach (ShelfSlot slot in allSlots)
        {
            if (slot.Matches(book) && slot.IsAvailable)
                return slot;
        }
        return null;
    }

    void PickUp(BookItem book)
    {
        // Eger bu kitap bir raftan geliyorsa (daha once yerlestirilmisse),
        // once o slotu bosalt ve istatistikleri geri al
        if (book.currentSlot != null)
        {
            book.currentSlot.RemoveBook(book);
        }

        book.SetHighlight(false);
        book.SetHeld(true);

        heldBooks.Add(book);
        RepositionHeldBooks();

        lookedBook = null;
    }

    // Elimizdeki TUM kitaplari, sag el noktasinda ust uste yigar (1'den 5'e kadar).
    void RepositionHeldBooks()
    {
        for (int i = 0; i < heldBooks.Count; i++)
        {
            BookItem book = heldBooks[i];
            book.transform.SetParent(rightHandPoint);

            float height = i * stackSpacing;
            book.transform.localPosition = new Vector3(0f, height, 0f);
            book.transform.localRotation = Quaternion.identity;

            // Elimizdeyken kucuk gozuksun diye boyutunu kucult (yerdeki/raftaki
            // gercek boyutu OriginalScale'de saklaniyor, bunu bozmuyoruz).
            // Y ekseni (kalinlik) ayrica bir miktar daha inceltiliyor,
            // genislik/uzunluk (X, Z) buna dahil degil.
            Vector3 baseScale = book.OriginalScale * heldScaleMultiplier;
            book.transform.localScale = new Vector3(
                baseScale.x,
                baseScale.y * heldThicknessMultiplier,
                baseScale.z
            );
        }
    }

    void DropTopBook()
    {
        BookItem book = heldBooks[heldBooks.Count - 1];
        heldBooks.RemoveAt(heldBooks.Count - 1);

        book.transform.SetParent(null);
        book.transform.localScale = book.OriginalScale; // yerde normal boyutuna don
        // Onumuze, biraz yukariya birak ki fizik ile yere dogal dussun
        Vector3 dropPos = transform.position + transform.forward * 0.8f + Vector3.up * 1f;
        book.transform.position = dropPos;
        book.transform.rotation = Random.rotation;

        book.SetHeld(false);

        RepositionHeldBooks(); // kalanlari (varsa) dogru yerlere tekrar diz
    }
}