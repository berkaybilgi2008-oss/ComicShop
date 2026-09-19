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
