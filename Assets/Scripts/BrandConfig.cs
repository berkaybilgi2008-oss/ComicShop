public static class BrandConfig
{
    private static BrandCatalog Catalog => UnityEngine.Resources.Load<BrandCatalog>("BrandCatalog");

    public static int BrandCount => Catalog != null ? Catalog.BrandCount : 0;
    public static int TotalBookTypeCount => Catalog != null ? Catalog.TotalBookCount : 0;

    public static int GetBrandForBookID(int bookID)
        => Catalog != null ? Catalog.GetBrandForBookID(bookID) : -1;

    public static int GetBookRangeStart(int brandID)
        => Catalog != null ? Catalog.GetBookRangeStart(brandID) : -1;

    public static string GetBrandName(int brandID)
        => Catalog != null ? Catalog.GetBrandName(brandID) : string.Empty;

    // Eski script API uyumlulugu.
    public static int[] booksPerBrand
    {
        get
        {
            if (Catalog == null || Catalog.brands == null) return System.Array.Empty<int>();
            var result = new int[Catalog.brands.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = Catalog.brands[i] != null ? Catalog.brands[i].bookCount : 0;
            return result;
        }
    }

    public static int[] heroesPerBrand => booksPerBrand;
    public static int TotalHeroCount => TotalBookTypeCount;
    public static int GetBrandForHero(int heroID) => GetBrandForBookID(heroID);
    public static int GetHeroRangeStart(int brandID) => GetBookRangeStart(brandID);
}
