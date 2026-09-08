using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// BUILD/TEST HILESI -- "Yaratik Gucu".
///
/// Elindeki kitabin sahnedeki BUTUN kopyalarini sana getirir. Raf gozlerini
/// doldururken 10 kopyayi haritada tek tek aramak yerine tek tusla topla.
///
/// KURULUM:
/// 1) Bu script'i Player prefab'ina (PlayerInteraction'in oldugu objeye) ekle.
/// 2) Multiplayer'da: NetworkPlayerSetup'taki "Owner Only Components" listesine
///    bu bileseni de surukle. Boylece sadece kendi karakterinde calisir.
/// 3) Oyunda elinde bir kitapla O tusuna bas.
///
/// Mevcut scriptlerin HICBIRINI degistirmez. PlayerInteraction'in kendi
/// toplama fonksiyonunu reflection ile cagirir, boylece kitaplar normal
/// animasyonuyla, dogru sirada, dogru olcekte eline gelir.
/// </summary>
[DisallowMultipleComponent]
public class YaratikGucu : MonoBehaviour
{
    public enum Mod
    {
        /// <summary>Kopyalar tek tek eline gelir, el dolunca kalani onune dokulur.</summary>
        ElimeGetir,

        /// <summary>Hepsi onune yigilir, sen normal sekilde toplarsin.</summary>
        OnumeDok
    }

    [Header("Tus")]
    public KeyCode tus = KeyCode.O;

    [Header("Davranis")]
    public Mod mod = Mod.ElimeGetir;

    [Tooltip("Raf gozlerine yerlestirilmis kopyalari da geri cagir. " +
             "Kapaliysa sadece yerde/ortalikta duran kopyalar gelir.")]
    public bool raftakileriDeAl = true;

    [Tooltip("0 = sinirsiz. Kac kopya cagrilacagini sinirlamak istersen yaz.")]
    [Min(0)] public int maksAdet = 0;

    [Tooltip("Acikken hile SADECE Editor'de ve Development Build'de calisir. " +
             "Yayin build'ine sizmaz. Kapatirsan her build'de acik olur.")]
    public bool sadeceEditorVeDevBuild = true;

    [Header("Onune Dokme Ayarlari")]
    [Tooltip("Kitaplarin dokulecegi noktanin oyuncudan uzakligi (metre).")]
    public float dokmeMesafesi = 1.3f;

    [Tooltip("Dokulen kitaplarin dagilma yaricapi (metre).")]
    public float dokmeYaricapi = 0.45f;

    [Tooltip("Dokulen kitaplarin birakilma yuksekligi (metre).")]
    public float dokmeYuksekligi = 0.9f;

    [Header("Debug")]
    public bool konsolaYaz = true;

    // ------------------------------------------------------------------

    private PlayerInteraction etkilesim;
    private bool calisiyor;

    private static MethodInfo pickUpMetodu;
    private static MethodInfo yerlesimTazeleMetodu;
    private static bool reflectionDenendi;

    void Awake()
    {
        etkilesim = GetComponent<PlayerInteraction>();
        if (etkilesim == null)
            etkilesim = GetComponentInParent<PlayerInteraction>();
    }

    void Update()
    {
        if (calisiyor || Cursor.lockState != CursorLockMode.Locked || !Input.GetKeyDown(tus))
            return;

        if (sadeceEditorVeDevBuild && !Application.isEditor && !Debug.isDebugBuild)
            return;

        if (etkilesim == null || !etkilesim.enabled)
            return;

        if (ConnectionManager.Instance != null && ConnectionManager.Instance.IsRunning)
        {
            var setup = GetComponent<NetworkPlayerSetup>();
            if (setup != null && setup.IsSpawned && setup.IsOwner && !setup.IsDown && etkilesim.ActiveHeldBook != null)
                setup.YaratikGucuRpc(etkilesim.ActiveHeldBook.bookID);
            return;
        }

        StartCoroutine(Cagir());
    }

    // ------------------------------------------------------------------

    private IEnumerator Cagir()
    {
        calisiyor = true;

        BookItem aktif = etkilesim.ActiveHeldBook;
        if (aktif == null)
        {
            Yaz("Elinde kitap yok. Once bir kitap al, sonra tusa bas.", true);
            calisiyor = false;
            yield break;
        }

        int hedefID = aktif.bookID;
        List<BookItem> kopyalar = KopyalariBul(hedefID, aktif);

        if (kopyalar.Count == 0)
        {
            Yaz($"Book {hedefID + 1} icin cagrilabilecek baska kopya bulunamadi " +
                $"(elindeki haric). Hepsi zaten sende olabilir.", true);
            calisiyor = false;
            yield break;
        }

        if (maksAdet > 0 && kopyalar.Count > maksAdet)
            kopyalar.RemoveRange(maksAdet, kopyalar.Count - maksAdet);

        int eleGelen = 0;
        int yereDokulen = 0;

        for (int i = 0; i < kopyalar.Count; i++)
        {
            BookItem kitap = kopyalar[i];
            if (kitap == null)
                continue;

            // Raftaysa once raftan dus (GameStats sayaci da boylece dogru kalir).
            if (kitap.currentSlot != null)
                kitap.currentSlot.RemoveBook(kitap);

            kitap.transform.SetParent(null, true);

            bool elimeAlindi = false;

            if (mod == Mod.ElimeGetir && ElYerVar())
            {
                // Kitabi once onumuze isinla ki toplama animasyonu haritanin
                // obur ucundan suzulup gelmesin.
                kitap.SetHeld(false);
                kitap.transform.position = OnumdekiNokta(0.8f, Vector3.zero);

                elimeAlindi = ElineVer(kitap);
                if (elimeAlindi)
                {
                    eleGelen++;
                    yield return new WaitForSeconds(Mathf.Max(0.02f, etkilesim.bookMoveDuration * 0.5f));
                }
            }

            if (!elimeAlindi)
            {
                Yere(kitap, yereDokulen);
                yereDokulen++;
            }
        }

        // Toplu alimdan sonra el duzenini bir kere oturt.
        YerlesimiTazele();

        string ozet = $"Yaratik Gucu -> Book {hedefID + 1}: {kopyalar.Count} kopya cagrildi";
        if (eleGelen > 0) ozet += $", {eleGelen} tanesi eline geldi";
        if (yereDokulen > 0) ozet += $", {yereDokulen} tanesi onune dokuldu (el doldu)";
        Yaz(ozet + ".", false);

        calisiyor = false;
    }

    // ------------------------------------------------------------------
    // Arama
    // ------------------------------------------------------------------

    private List<BookItem> KopyalariBul(int hedefID, BookItem haric)
    {
        BookItem[] hepsi = Object.FindObjectsByType<BookItem>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        List<BookItem> sonuc = new List<BookItem>(hepsi.Length);
        Vector3 benimYerim = transform.position;

        foreach (BookItem kitap in hepsi)
        {
            if (kitap == null || kitap == haric)
                continue;

            if (kitap.bookID != hedefID)
                continue;

            // Birinin (senin ya da baska oyuncunun) elindekini caliyor gibi olmayalim.
            if (kitap.IsHeld)
                continue;

            if (kitap.currentSlot != null && !raftakileriDeAl)
                continue;

            sonuc.Add(kitap);
        }

        // Once yakindakiler gelsin.
        sonuc.Sort((a, b) =>
            (a.transform.position - benimYerim).sqrMagnitude
            .CompareTo((b.transform.position - benimYerim).sqrMagnitude));

        return sonuc;
    }

    private bool ElYerVar()
    {
        return etkilesim.HeldBooksList.Count < etkilesim.MaxHeldBooks;
    }

    // ------------------------------------------------------------------
    // Yerlestirme
    // ------------------------------------------------------------------

    private void Yere(BookItem kitap, int sira)
    {
        // Cember seklinde dagit, ust uste binip zipla-zipla ucmasinlar.
        float aci = sira * 137.5f * Mathf.Deg2Rad;          // altin aci = duzgun dagilim
        float yaricap = dokmeYaricapi * Mathf.Sqrt((sira + 1) / 8f);
        Vector3 sapma = new Vector3(Mathf.Cos(aci), 0f, Mathf.Sin(aci)) * Mathf.Min(yaricap, dokmeYaricapi);

        kitap.transform.position = OnumdekiNokta(1f, sapma) + Vector3.up * (sira * 0.015f);
        kitap.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * kitap.NativeRotation;
        kitap.transform.localScale = kitap.OriginalScale;

        kitap.SetHeld(false);   // rigidbody'yi normale dondurur, hiz sifirlanir
    }

    private Vector3 OnumdekiNokta(float mesafeCarpani, Vector3 sapma)
    {
        Vector3 ileri = transform.forward;
        ileri.y = 0f;
        if (ileri.sqrMagnitude < 0.0001f)
            ileri = Vector3.forward;
        ileri.Normalize();

        return transform.position
             + ileri * (dokmeMesafesi * mesafeCarpani)
             + Vector3.up * dokmeYuksekligi
             + sapma;
    }

    // ------------------------------------------------------------------
    // Reflection: PlayerInteraction'a dokunmadan onun kendi fonksiyonunu kullan
    // ------------------------------------------------------------------

    private bool ElineVer(BookItem kitap)
    {
        MethodInfo metot = PickUpMetodunuBul();
        if (metot == null)
            return false;

        metot.Invoke(etkilesim, new object[] { kitap });
        return true;
    }

    private void YerlesimiTazele()
    {
        if (yerlesimTazeleMetodu == null)
            return;

        yerlesimTazeleMetodu.Invoke(etkilesim, null);
    }

    private MethodInfo PickUpMetodunuBul()
    {
        if (reflectionDenendi)
            return pickUpMetodu;

        reflectionDenendi = true;

        const BindingFlags bayraklar = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        pickUpMetodu = typeof(PlayerInteraction).GetMethod(
            "PickUp", bayraklar, null, new[] { typeof(BookItem) }, null);

        yerlesimTazeleMetodu = typeof(PlayerInteraction).GetMethod(
            "RepositionHeldBooksImmediate", bayraklar);

        if (pickUpMetodu == null)
        {
            Debug.LogError(
                "[YaratikGucu] PlayerInteraction icinde 'PickUp(BookItem)' bulunamadi. " +
                "Fonksiyon adi degismis olabilir. Mod'u 'OnumeDok' yap ya da " +
                "PlayerInteraction'a su satirlari ekle:\n\n" +
                "    public void ZorlaAl(BookItem book) { PickUp(book); }\n", this);
        }

        return pickUpMetodu;
    }

    // ------------------------------------------------------------------

    private void Yaz(string mesaj, bool uyari)
    {
        if (!konsolaYaz)
            return;

        if (uyari) Debug.LogWarning("[YaratikGucu] " + mesaj, this);
        else Debug.Log("[YaratikGucu] " + mesaj, this);
    }
}
