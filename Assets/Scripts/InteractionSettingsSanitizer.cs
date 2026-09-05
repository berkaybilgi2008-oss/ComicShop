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

            // Eski sahne serilestirmesi mouse yerine E/Q kaydetmis olabilir.
            interaction.pickupKey = KeyCode.Mouse0;
            interaction.dropKey = KeyCode.Mouse1;

            // Elde kitaplar arasindaki dikey ilerleme 0.044 birim olsun.
            interaction.stackSpacing = 0.044f;
            interaction.heldScaleMultiplier = 0.55f;
        }
    }
}
