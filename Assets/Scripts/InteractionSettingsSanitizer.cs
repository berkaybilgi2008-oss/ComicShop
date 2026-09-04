using UnityEngine;

/// <summary>
/// Eski sahne serilestirmesinden kalan PlayerInteraction degerlerini,
/// gelistirilmis varsayilanlarla uyumlu hale getirir.
/// Sahne dosyasina dokunmadan calisma zamaninda tek seferlik uyumluluk saglar.
/// </summary>
public static class InteractionSettingsSanitizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Sanitize()
    {
        PlayerInteraction[] interactions = Object.FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None);

        foreach (PlayerInteraction interaction in interactions)
        {
            if (interaction == null)
                continue;

            // Bu iki deger mevcut sahnede eski surumden kalmis durumda.
            // Yeni sistemin 3 birim stack araligi ve 0.55 olcegiyle calismasi gerekir.
            if (interaction.stackSpacing > 0f && interaction.stackSpacing < 0.5f && interaction.heldScaleMultiplier > 0.65f)
            {
                interaction.stackSpacing = 3f;
                interaction.heldScaleMultiplier = 0.55f;
            }
        }
    }
}
