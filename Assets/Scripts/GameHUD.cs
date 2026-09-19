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
        if (hudText == null) return;
        if (playerInteraction == null) { hudText.text = ""; return; }

        sb.Clear();
        sb.Append(GameStats.CompletedBookGroupCount).Append("/").Append(GameStats.totalBookTypes)
          .Append(" (kitap grubu)
");
        sb.Append(GameStats.TotalPlaced).Append("/").Append(GameStats.TotalBooks)
          .Append(" (kitap)
");
        sb.Append(playerInteraction.HeldBooksList.Count).Append("/").Append(playerInteraction.MaxHeldBooks)
          .Append(" (elde)
");
        sb.Append("
");

        sb.Append("<size=70%>");

        // PlayerInteraction'da aktif kitap stack'in EN USTUNDE tutulur ve
        // firlatilan kitap da her zaman bu kitaptir. HUD da ayni sirayi kullanir:
        // ilk satir = su an elde en ustte olan / Q ile firlatilacak kitap.
        int activeIndex = playerInteraction.ActiveHeldIndex;
        if (activeIndex >= 0 && activeIndex < playerInteraction.HeldBooksList.Count)
        {
            AppendBookLine(playerInteraction.HeldBooksList[activeIndex], true);

            for (int i = 0; i < playerInteraction.HeldBooksList.Count; i++)
            {
                if (i == activeIndex)
                    continue;

                AppendBookLine(playerInteraction.HeldBooksList[i], false);
            }
        }
        else
        {
            for (int i = 0; i < playerInteraction.HeldBooksList.Count; i++)
                AppendBookLine(playerInteraction.HeldBooksList[i], false);
        }

        sb.Append("</size>");
        string hint = playerInteraction.InteractionHint;
        if (!string.IsNullOrEmpty(hint)) sb.Append("
").Append(hint);
        hudText.text = sb.ToString();
    }

    private void AppendBookLine(BookItem book, bool active)
    {
        if (book == null)
            return;

        if (active)
            sb.Append("<color=#FFFF00>");

        sb.Append("_").Append(book.DisplayName);

        if (active)
            sb.Append("</color>");

        sb.Append("
");
    }
}
