using UnityEngine;

[CreateAssetMenu(menuName = "ComicShop/Book Catalog", fileName = "BookCatalog")]
public class BookCatalog : ScriptableObject
{
    [Tooltip("All generated books. BookID is unique but does not need to be contiguous; BrandID is authoritative.")]
    public BookData[] books;
}
