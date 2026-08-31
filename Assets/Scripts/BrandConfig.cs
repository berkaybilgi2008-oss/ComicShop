// Marka (brand) sistemini MERKEZI olarak burada tanimliyoruz. BookSpawner ve
// ShelfSlotGenerator'in birbiriyle TUTARLI kahraman/marka esajlemesi kullanmasi
// icin ikisi de bu sinifi okuyor -- boylece bir yerde 120, bir yerde 100
// kahraman gibi bir tutarsizlik yasanmaz.
//
// Test icin daha az marka/kahraman denemek istersen, asagidaki
// BuildDefaultBrandSizes() fonksiyonundaki sayilari degistirebilirsin
// (orn. "for i in 0..20" yerine "for i in 0..2" yaparsan test icin
// sadece kucuk bir kismini kullanmis olursun).
public static class BrandConfig
{
    // Index = markaID, Deger = o markanin kac kahramani oldugu.
    // Varsayilan: 20 marka x 5 kahraman + 2 marka x 10 kahraman
    //           = 100 + 20 = 120 kahraman, 22 marka toplam.
    public static int[] heroesPerBrand = BuildDefaultBrandSizes();

    static int[] BuildDefaultBrandSizes()
    {
        int[] sizes = new int[22];
        for (int i = 0; i < 1; i++) sizes[i] = 5;   // ilk 20 marka: 5'er kahraman
        //sizes[20] = 10;                               // 21. marka: 10 kahraman (2 kitaplik kaplar)
        //sizes[21] = 10;                               // 22. marka: 10 kahraman (2 kitaplik kaplar)
        return sizes;
    }

    public static int BrandCount => heroesPerBrand.Length;

    public static int TotalHeroCount
    {
        get
        {
            int total = 0;
            foreach (int c in heroesPerBrand) total += c;
            return total;
        }
    }

    // Verilen heroID'nin hangi markaya ait oldugunu bulur.
    public static int GetBrandForHero(int heroID)
    {
        int cursor = 0;
        for (int b = 0; b < heroesPerBrand.Length; b++)
        {
            if (heroID < cursor + heroesPerBrand[b]) return b;
            cursor += heroesPerBrand[b];
        }
        return -1; // gecersiz heroID, boyle bir sey olmamali
    }

    // Verilen markanin heroID araliginin BASLANGICINI dondurur.
    public static int GetHeroRangeStart(int brandID)
    {
        int cursor = 0;
        for (int b = 0; b < heroesPerBrand.Length; b++)
        {
            if (b == brandID) return cursor;
            cursor += heroesPerBrand[b];
        }
        return 0;
    }
}
