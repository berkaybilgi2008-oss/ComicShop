public static class BrandConfig
{
    // 22 marka: ilk 20 marka 15 kitap, son 2 marka 30 kitap.
    // Toplam: 360 farkli kitap turu.
    public static int[] booksPerBrand = BuildDefaultBrandSizes();

    public static int BrandCount => booksPerBrand.Length;

    public static int TotalBookTypeCount
    {
        get
        {
            int total = 0;
            foreach (int count in booksPerBrand)
                total += count;
            return total;
        }
    }

    public static int GetBrandForBookID(int bookID)
    {
        int cursor = 0;
        for (int brand = 0; brand < booksPerBrand.Length; brand++)
        {
            if (bookID >= cursor && bookID < cursor + booksPerBrand[brand])
                return brand;
            cursor += booksPerBrand[brand];
        }
        return -1;
    }

    public static int GetBookRangeStart(int brandID)
    {
        if (brandID < 0 || brandID >= booksPerBrand.Length)
            return -1;

        int cursor = 0;
        for (int brand = 0; brand < brandID; brand++)
            cursor += booksPerBrand[brand];
        return cursor;
    }

    // Eski scriptlerin derlenmesini koruyan gecici uyumluluk isimleri.
    public static int[] heroesPerBrand => booksPerBrand;
    public static int TotalHeroCount => TotalBookTypeCount;
    public static int GetBrandForHero(int heroID) => GetBrandForBookID(heroID);
    public static int GetHeroRangeStart(int brandID) => GetBookRangeStart(brandID);

    private static int[] BuildDefaultBrandSizes()
    {
        int[] sizes = new int[22];
        for (int i = 0; i < 20; i++)
            sizes[i] = 15;

        sizes[20] = 30;
        sizes[21] = 30;
        return sizes;
    }
}
