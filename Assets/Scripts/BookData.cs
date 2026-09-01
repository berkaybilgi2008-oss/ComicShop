using UnityEngine;

[CreateAssetMenu(menuName = "ComicShop/Book Data", fileName = "BookData")]
public class BookData : ScriptableObject
{
    [Header("Kimlik")]
    [Min(0)]
    public int BookID;

    [Min(0)]
    public int BrandID;

    [Header("Gorsel Prefab")]
    [Tooltip("Bu kitabin fiziksel model prefab'i. Her farkli kitap kendi prefab'ini kullanabilir.")]
    public GameObject bookPrefab;
}
