using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Bir oyuncu prefab'i her istemcide olusturulur -- 4 kisilik oyunda herkesin
/// makinesinde 4 tane Player vardir. Ama sen sadece KENDI karakterini
/// kontrol etmelisin, digerlerini sadece gormelisin.
///
/// Bu bilesen tam olarak bunu yapar: sahibi olmayan kopyalarda kamerayi,
/// girdi scriptlerini ve ekran arayuzunu kapatir.
///
/// Mevcut PlayerController / PlayerInteraction dosyalarina DOKUNMAZ.
/// Calisan kodu bozma riski olmasin diye bilerek boyle kuruldu.
///
/// KURULUM: Player prefab'ina ekle. Alanlari bos birakabilirsin, kendisi bulur.
/// </summary>
public class NetworkPlayerSetup : NetworkBehaviour
{
    [Header("Referanslar (bos birakilirsa otomatik bulunur)")]
    [Tooltip("Bu oyuncunun kamerasi. Sahibi olmayanlarda kapatilir.")]
    public Camera playerCamera;

    [Tooltip("Sahibi olmayanlarda kapatilir -- yoksa 4 dinleyici ust uste biner.")]
    public AudioListener audioListener;

    [Header("Ek Bilesenler")]
    [Tooltip("Sadece sahibinde calismasi gereken BASKA bilesenler varsa buraya ekle " +
             "(orn. PrankOverlay). PlayerController, PlayerInteraction ve Crosshair " +
             "zaten otomatik bulunuyor.")]
    public MonoBehaviour[] ownerOnlyComponents;

    [Header("HUD")]
    [Tooltip("Sahibi olan oyuncu, sahnedeki GameHUD'a kendini bagalasin mi?")]
    public bool connectHudToLocalPlayer = true;

    public override void OnNetworkSpawn()
    {
        ResolveReferences();

        if (IsOwner)
            SetUpLocalPlayer();
        else
            SetUpRemotePlayer();
    }

    private void ResolveReferences()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (audioListener == null)
            audioListener = GetComponentInChildren<AudioListener>(true);
    }

    // ------------------------------------------------------------------

    /// <summary>Bu bizim karakterimiz: her sey acik, HUD'a baglan.</summary>
    private void SetUpLocalPlayer()
    {
        SetOwnerComponentsEnabled(true);

        if (playerCamera != null)
            playerCamera.enabled = true;

        if (audioListener != null)
            audioListener.enabled = true;

        gameObject.name = $"Player_LOCAL_{OwnerClientId}";

        if (connectHudToLocalPlayer)
            ConnectHud();

        Debug.Log($"[Ag] Kendi karakterin hazir (ID {OwnerClientId}).");
    }

    /// <summary>Bu baskasinin karakteri: sadece gorunsun, kontrol edilmesin.</summary>
    private void SetUpRemotePlayer()
    {
        SetOwnerComponentsEnabled(false);

        if (playerCamera != null)
            playerCamera.enabled = false;

        if (audioListener != null)
            audioListener.enabled = false;

        // CharacterController uzak kopyada calisirsa ag pozisyonuyla kavga eder.
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        gameObject.name = $"Player_{OwnerClientId}";

        Debug.Log($"[Ag] Uzak oyuncu goruntulenecek (ID {OwnerClientId}).");
    }

    private void SetOwnerComponentsEnabled(bool enabled)
    {
        SetEnabled(GetComponent<PlayerController>(), enabled);
        SetEnabled(GetComponent<PlayerInteraction>(), enabled);
        SetEnabled(GetComponent<Crosshair>(), enabled);

        if (ownerOnlyComponents == null)
            return;

        foreach (MonoBehaviour component in ownerOnlyComponents)
            SetEnabled(component, enabled);
    }

    private static void SetEnabled(Behaviour component, bool enabled)
    {
        if (component != null)
            component.enabled = enabled;
    }

    /// <summary>Sahnedeki GameHUD, yerel oyuncuyu gostersin.</summary>
    private void ConnectHud()
    {
        GameHUD hud = FindFirstObjectByType<GameHUD>();

        if (hud == null)
            return;

        PlayerInteraction interaction = GetComponent<PlayerInteraction>();

        if (interaction != null)
            hud.playerInteraction = interaction;
    }
}
