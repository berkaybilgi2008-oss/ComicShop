using System.Text;
using UnityEngine;
using TMPro;

public class GameHUD : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Sahnedeki Player objesindeki Player Interaction script'i")]
    public PlayerInteraction playerInteraction;
    [Tooltip("Ekrana yazi basacak TextMeshProUGUI (Canvas icindeki Text objesi)")]
    public TMP_Text hudText;

    private StringBuilder sb = new StringBuilder();

    void Update()
    {
        if (hudText == null || playerInteraction == null) return;

        sb.Clear();
        // GECICI TEST: sprite etiketleri kaldirildi, sadece duz metin var
        sb.Append(GameStats.CompletedSeriesCount).Append("/").Append(GameStats.totalHeroes)
          .Append(" (seri)\n");
        sb.Append(GameStats.TotalPlaced).Append("/").Append(GameStats.TotalBooks)
          .Append(" (kitap)\n");
        sb.Append(playerInteraction.HeldBooksList.Count).Append("/").Append(playerInteraction.MaxHeldBooks)
          .Append(" (elde)\n");
        sb.Append("\n");

        // Kitap isimleri listesini daha kucuk bir font ile yaziyoruz (ustteki
        // sayaclardan daha az onemli oldugu icin gorsel olarak da kucuk kalsin)
        sb.Append("<size=70%>");
        foreach (BookItem book in playerInteraction.HeldBooksList)
        {
            sb.Append("_").Append(book.DisplayName).Append("\n");
        }
        sb.Append("</size>");

        hudText.text = sb.ToString();
    }
}