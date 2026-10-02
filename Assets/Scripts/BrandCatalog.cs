using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ComicShop/Brand Catalog", fileName = "BrandCatalog")]
public class BrandCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public int brandID;
        public string brandName;
        public int bookCount;
        [Tooltip("Raf tabelasinda kullanilan logo dokusu.")]
        public Texture2D logoTexture;
    }

    public Entry[] brands = Array.Empty<Entry>();

    public int BrandCount => brands != null ? brands.Length : 0;
    public int TotalBookCount
    {
        get
        {
            int total = 0;
            if (brands == null) return 0;
            foreach (var brand in brands)
                if (brand != null) total += Mathf.Max(0, brand.bookCount);
            return total;
        }
    }

    // Exact asset identity: filenames and book cover textures are not publisher IDs.
    public int GetBrandForLogo(Texture logo)
    {
        if (logo == null || brands == null) return -1;
        int match = -1;
        foreach (var brand in brands)
        {
            if (brand == null || brand.logoTexture != logo) continue;
            if (brand.brandID < 0 || match >= 0) return -1;
            match = brand.brandID;
        }
        return match;
    }

    public string GetBrandName(int brandID)
    {
        if (brands == null) return string.Empty;
        foreach (var brand in brands)
            if (brand != null && brand.brandID == brandID) return brand.brandName;
        return string.Empty;
    }

    public int GetBrandForBookID(int bookID)
    {
        if (brands == null || bookID < 0) return -1;
        foreach (var brand in brands)
        {
            if (brand == null) continue;
            int start = GetBookRangeStart(brand.brandID);
            if (bookID >= start && bookID < start + Mathf.Max(0, brand.bookCount))
                return brand.brandID;
        }
        return -1;
    }

    public int GetBookRangeStart(int brandID)
    {
        if (brands == null) return -1;
        int cursor = 0;
        foreach (var brand in brands)
        {
            if (brand == null) continue;
            if (brand.brandID == brandID) return cursor;
            cursor += Mathf.Max(0, brand.bookCount);
        }
        return -1;
    }
}
