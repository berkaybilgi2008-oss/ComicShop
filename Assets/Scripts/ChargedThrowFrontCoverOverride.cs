using System.Reflection;
using UnityEngine;

/// <summary>
/// Sarjli atis pozunda mevcut kapak yonunun tam tersini uygular.
/// PlayerInteraction'a dokunmadan, GetThrowPose sonrasinda LateUpdate'te calisir.
/// </summary>
public class ChargedThrowFrontCoverOverride : MonoBehaviour
{
    private PlayerInteraction interaction;
    private FieldInfo chargingField;
    private FieldInfo chargeAmountField;
    private bool wasCharging;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallOnPlayers()
    {
        PlayerInteraction[] players = Object.FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None);
        foreach (PlayerInteraction player in players)
        {
            if (player != null && player.GetComponent<ChargedThrowFrontCoverOverride>() == null)
                player.gameObject.AddComponent<ChargedThrowFrontCoverOverride>();
        }
    }

    void Awake()
    {
        interaction = GetComponent<PlayerInteraction>();
        if (interaction == null)
            return;

        System.Type type = typeof(PlayerInteraction);
        chargingField = type.GetField("isChargingThrow", BindingFlags.Instance | BindingFlags.NonPublic);
        chargeAmountField = type.GetField("chargeAmount", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    void LateUpdate()
    {
        if (interaction == null || chargingField == null || chargeAmountField == null || interaction.ActiveHeldBook == null)
            return;

        bool isCharging = (bool)chargingField.GetValue(interaction);
        if (!isCharging)
        {
            wasCharging = false;
            return;
        }

        BookItem book = interaction.ActiveHeldBook;
        if (book == null)
            return;

        float charge = (float)chargeAmountField.GetValue(interaction);
        float angle = Mathf.Lerp(interaction.windupStartAngle, interaction.windupFullAngle, charge);

        Transform cam = interaction.playerCamera != null ? interaction.playerCamera.transform : null;
        if (cam == null)
            return;

        // GetThrowPose ile ayni boy eksenini kullaniyoruz.
        Vector3 coverNormal = cam.right;
        Vector3 armDirection = Quaternion.AngleAxis(angle, coverNormal) * cam.up;

        // Onceki 180 derece duzeltmenin tam tersini uygula.
        // Boylece x=270 ve x=0 authored rotasyon gruplarinin ikisi de
        // mevcut sistemin ters kapak tarafina gecmis olur.
        book.transform.rotation = Quaternion.AngleAxis(180f, armDirection) * book.transform.rotation;
        wasCharging = true;
    }
}
