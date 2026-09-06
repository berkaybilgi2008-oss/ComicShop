using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Baglanti islerinin TEK adresi. Oyun mantigi burayla konusur, NetworkManager
/// ile dogrudan konusmaz.
///
/// NEDEN AYRI DOSYA: Steam'e gecerken sadece bu dosya degisecek. Baglanma kodu
/// menuye, oyuncuya, HUD'a dagilirsa hepsini tek tek bulmak gerekir.
/// Simdi UnityTransport (IP) kullaniyoruz, Steam'de SteamID kullanacagiz --
/// StartHost/StartClient/Disconnect imzalari aynen kalacak.
///
/// KURULUM: NetworkManager objesine bu script'i ekle.
/// </summary>
[RequireComponent(typeof(NetworkManager))]
public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance { get; private set; }

    [Header("Baglanti (gelistirme)")]
    [Tooltip("Client olarak baglanilacak adres. Ayni bilgisayarda test icin 127.0.0.1")]
    public string address = "127.0.0.1";
    [Tooltip("Port. Degistirmeye gerek yok.")]
    public ushort port = 7777;

    [Header("Test Arayuzu")]
    [Tooltip("Ekranin sol ustunde Host/Client butonlari gosterir. " +
             "Gercek menu yapilinca kapatilacak.")]
    public bool showDebugUI = true;

    private NetworkManager networkManager;

    /// <summary>Su an bir oturuma bagli miyiz?</summary>
    public bool IsRunning =>
        networkManager != null && (networkManager.IsHost || networkManager.IsServer || networkManager.IsClient);

    /// <summary>Baglanti durumu degistiginde haber verir. UI buna abone olabilir.</summary>
    public event Action<string> OnStatusChanged;

    private string status = "Bagli degil";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        networkManager = GetComponent<NetworkManager>();
    }

    void OnEnable()
    {
        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback += HandleClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    void OnDisable()
    {
        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback -= HandleClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    // ------------------------------------------------------------------
    // Dis dunyaya acilan API -- Steam'e gecerken bu imzalar DEGISMEYECEK
    // ------------------------------------------------------------------

    /// <summary>Oda kurar. Host hem sunucu hem oyuncudur.</summary>
    public bool StartHost()
    {
        if (!Prepare(out UnityTransport transport))
            return false;

        transport.SetConnectionData("0.0.0.0", port);

        if (networkManager.StartHost())
        {
            SetStatus($"Oda kuruldu (port {port})");
            return true;
        }

        SetStatus("Oda kurulamadi");
        return false;
    }

    /// <summary>Var olan bir odaya katilir.</summary>
    public bool StartClient()
    {
        if (!Prepare(out UnityTransport transport))
            return false;

        transport.SetConnectionData(address, port);

        if (networkManager.StartClient())
        {
            SetStatus($"Baglaniliyor: {address}:{port}");
            return true;
        }

        SetStatus("Baglanilamadi");
        return false;
    }

    /// <summary>Oturumdan ayrilir.</summary>
    public void Disconnect()
    {
        if (networkManager == null || !IsRunning)
            return;

        networkManager.Shutdown();
        SetStatus("Baglanti kesildi");
    }

    // ------------------------------------------------------------------

    private bool Prepare(out UnityTransport transport)
    {
        transport = null;

        if (networkManager == null)
        {
            Debug.LogError("ConnectionManager: NetworkManager bulunamadi.");
            return false;
        }

        if (IsRunning)
        {
            Debug.LogWarning("ConnectionManager: zaten bir oturum var, once Disconnect cagir.");
            return false;
        }

        transport = networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError("ConnectionManager: NetworkManager uzerinde UnityTransport yok. " +
                           "Add Component > Unity Transport ekle ve NetworkManager'in " +
                           "Network Transport alanina surukle.");
            return false;
        }

        return true;
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (networkManager.LocalClientId == clientId)
            SetStatus($"Baglandi (ID {clientId})");
        else
            SetStatus($"Oyuncu katildi (ID {clientId}) -- toplam {networkManager.ConnectedClientsIds.Count}");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (networkManager.LocalClientId == clientId)
            SetStatus("Baglanti koptu");
        else
            SetStatus($"Oyuncu ayrildi (ID {clientId})");
    }

    private void SetStatus(string text)
    {
        status = text;
        Debug.Log($"[Baglanti] {text}");
        OnStatusChanged?.Invoke(text);
    }

    // ------------------------------------------------------------------
    // Gecici test arayuzu -- gercek menu yapilinca silinecek
    // ------------------------------------------------------------------

    void OnGUI()
    {
        if (!showDebugUI)
            return;

        const float w = 210f;
        GUILayout.BeginArea(new Rect(12f, 12f, w, 190f), GUI.skin.box);

        GUILayout.Label($"<b>Ag Durumu</b>\n{status}",
            new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });

        GUILayout.Space(6f);

        if (!IsRunning)
        {
            if (GUILayout.Button("Oda Kur (Host)", GUILayout.Height(28f)))
                StartHost();

            GUILayout.Space(4f);
            GUILayout.Label("Adres:");
            address = GUILayout.TextField(address);

            if (GUILayout.Button("Katil (Client)", GUILayout.Height(28f)))
                StartClient();
        }
        else
        {
            GUILayout.Label($"Rol: {(networkManager.IsHost ? "Host" : "Client")}");
            GUILayout.Label($"Oyuncu: {networkManager.ConnectedClientsIds.Count}");

            GUILayout.Space(4f);
            if (GUILayout.Button("Ayril", GUILayout.Height(26f)))
                Disconnect();
        }

        GUILayout.EndArea();
    }
}
