using UnityEngine;

[DisallowMultipleComponent]
public sealed class BookDisplayName : MonoBehaviour
{
    [Tooltip("Oyuncunun elde tuttugu kitap listesinde ve HUD'da gorunecek isim.")]
    [SerializeField] private string bookName = "";

    public string Name => string.IsNullOrWhiteSpace(bookName) ? "" : bookName.Trim();

    public void SetName(string value) => bookName = value ?? "";

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (bookName != null)
            bookName = bookName.Trim();
    }
#endif
}
