using UnityEngine;

[CreateAssetMenu(menuName = "ComicShop/Book Data", fileName = "BookData")]
public class BookData : ScriptableObject
{
    [Min(0)]
    public int BookID;

    [Min(0)]
    public int BrandID;
}
