using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public class ConnectionManager : MonoBehaviour
{
    public enum SessionState { Idle, StartingHost, Connecting, Connected, Disconnecting }
    public static ConnectionManager Instance { get; private set; }
    public string address = "127.0.0.1";
    public ushort port = 7777;
    public bool showDebugUI = true;
    [Min(1f)] public float connectionTimeout = 15f;
    [Range(1, 4)] public int maxPlayers = 4;
    public SessionState State { get; private set; }
    public bool IsRunning => State != SessionState.Idle || TransportBusy;
    public event Action<string> OnStatusChanged;

    private NetworkManager networkManager;
    private string status = "Bagli degil";
    private float attemptStarted;
    private bool TransportBusy => networkManager != null &&
        (networkManager.IsListening || networkManager.IsClient || networkManager.IsServer ||
         networkManager.ShutdownInProgress);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }
        Instance = this;
        networkManager = GetComponent<NetworkManager>();
        // MPP windows must keep processing transport messages when focus changes.
        Application.runInBackground = true;
        SetCursor(false);
    }

    private void OnEnable()
    {
        if (networkManager == null) return;
        networkManager.OnClientConnectedCallback += HandleConnected;
        networkManager.OnClientDisconnectCallback += HandleDisconnected;
        networkManager.OnServerStarted += HandleServerStarted;
        networkManager.OnClientStopped += HandleStopped;
        networkManager.OnServerStopped += HandleStopped;
        networkManager.OnTransportFailure += HandleTransportFailure;
        networkManager.ConnectionApprovalCallback += ApproveConnection;
    }

    private void OnDisable()
    {
        if (networkManager == null) return;
        networkManager.OnClientConnectedCallback -= HandleConnected;
        networkManager.OnClientDisconnectCallback -= HandleDisconnected;
        networkManager.OnServerStarted -= HandleServerStarted;
        networkManager.OnClientStopped -= HandleStopped;
        networkManager.OnServerStopped -= HandleStopped;
        networkManager.OnTransportFailure -= HandleTransportFailure;
        networkManager.ConnectionApprovalCallback -= ApproveConnection;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnApplicationQuit()
    {
        if (networkManager != null && TransportBusy) networkManager.Shutdown(true);
    }

    public bool StartHost() => StartSession(true);
    public bool StartClient() => StartSession(false);

    private bool StartSession(bool host)
    {
        if (networkManager == null || IsRunning)
        {
            SetStatus("Once mevcut baglantinin kapanmasini bekle.");
            return false;
        }
        var transport = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
        if (transport == null || networkManager.NetworkConfig.PlayerPrefab == null)
        {
            SetStatus("NetworkManager: UnityTransport veya Player Prefab eksik.");
            return false;
        }
        if (!host && string.IsNullOrWhiteSpace(address))
        {
            SetStatus("Baglanilacak adresi gir.");
            return false;
        }
        try
        {
            ShelfSlot.BuildNetworkRegistry();
            foreach (var spawner in FindObjectsByType<BookSpawner>(FindObjectsSortMode.None))
                spawner.PrepareSession(networkManager);
            foreach (var machine in FindObjectsByType<BookRecallMachine>(FindObjectsSortMode.None))
                machine.ResetSession();

            networkManager.NetworkConfig.ConnectionApproval = true;
            // Remote address and server listen address are distinct UTP settings.
            transport.SetConnectionData(host ? "127.0.0.1" : address.Trim(), port,
                host ? "0.0.0.0" : null);
            State = host ? SessionState.StartingHost : SessionState.Connecting;
            attemptStarted = Time.realtimeSinceStartup;
            SetStatus(host ? $"Oda kuruluyor (port {port})" : $"Baglaniliyor: {address}:{port}");
            bool started = host ? networkManager.StartHost() : networkManager.StartClient();
            if (!started)
                StopWithStatus(host ? $"Oda acilamadi. Port {port} baska bir host tarafindan kullaniliyor olabilir."
                    : "Baglanti baslatilamadi.");
            return started;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            StopWithStatus($"Baglanti hatasi: {exception.Message}");
            return false;
        }
    }

    public void Disconnect() => StopWithStatus("Baglanti kapatiliyor...");

    private void StopWithStatus(string message)
    {
        State = SessionState.Disconnecting;
        SetCursor(false);
        SetStatus(message);
        if (networkManager != null && !networkManager.ShutdownInProgress)
            networkManager.Shutdown();
    }

    private void Update()
    {
        // Stopped callbacks run inside shutdown. Wait for NGO to dispose its transport.
        if (State == SessionState.Disconnecting && !TransportBusy)
        {
            State = SessionState.Idle;
            SetCursor(false);
            SetStatus(status + " Yeni oturum acabilirsin.");
        }
        if ((State == SessionState.Connecting || State == SessionState.StartingHost) &&
            Time.realtimeSinceStartup - attemptStarted >= connectionTimeout)
            StopWithStatus("Baglanti zaman asimina ugradi. Host ve adresi kontrol et.");
        if (State == SessionState.Connected && Input.GetKeyDown(KeyCode.Escape))
            SetCursor(Cursor.lockState != CursorLockMode.Locked);
    }

    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        maxPlayers = Mathf.Clamp(maxPlayers, 1, 4);
        response.Approved = networkManager.ConnectedClientsIds.Count < maxPlayers;
        response.CreatePlayerObject = response.Approved;
        response.Pending = false;
        response.Reason = response.Approved ? "" : "Oda dolu.";
        Transform spawn = networkManager.NetworkConfig.PlayerPrefab.transform;
        response.Position = spawn.position + spawn.right * (1.2f * (request.ClientNetworkId % (ulong)maxPlayers));
        response.Rotation = spawn.rotation;
    }

    private void HandleServerStarted()
    {
        foreach (var spawner in FindObjectsByType<BookSpawner>(FindObjectsSortMode.None))
            spawner.SpawnSession();
    }

    private void HandleConnected(ulong id)
    {
        if (id == networkManager.LocalClientId)
        {
            State = SessionState.Connected;
            SetStatus(networkManager.IsHost ? $"Oda acik (port {port})" : "Odaya katildin.");
        }
        else if (networkManager.IsServer) SetStatus($"Oyuncu katildi (ID {id}).");
    }

    private void HandleDisconnected(ulong id)
    {
        if (networkManager.IsServer && id != networkManager.LocalClientId)
        {
            NetworkBook.ReleaseAllForPlayer(id);
            SetStatus($"Oyuncu ayrildi (ID {id}).");
            return;
        }
        if (State == SessionState.Disconnecting) return;
        string reason = networkManager.DisconnectReason;
        StopWithStatus(string.IsNullOrEmpty(reason) ? "Baglanti kesildi." : reason);
    }

    private void HandleStopped(bool wasHost)
    {
        State = SessionState.Disconnecting;
        SetCursor(false);
    }

    private void HandleTransportFailure() => StopWithStatus($"Ag hatasi. Adres ve port {port} ayarini kontrol et.");

    public static void SetCursor(bool gameplay)
    {
        Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !gameplay;
    }

    private void SetStatus(string message)
    {
        status = message;
        Debug.Log($"[Baglanti] {message}");
        OnStatusChanged?.Invoke(message);
    }

    private void OnGUI()
    {
        if (!showDebugUI || (State == SessionState.Connected && Cursor.lockState == CursorLockMode.Locked)) return;
        GUILayout.BeginArea(new Rect(12, 12, 290, 250), GUI.skin.box);
        GUILayout.Label(status, new GUIStyle(GUI.skin.label) { wordWrap = true });
        if (!IsRunning)
        {
            if (GUILayout.Button("Oda Kur (Host)", GUILayout.Height(30))) StartHost();
            GUILayout.Label("Host adresi:");
            address = GUILayout.TextField(address);
            if (GUILayout.Button("Katil (Client)", GUILayout.Height(30))) StartClient();
        }
        else
        {
            // Never assume the server's client collection is populated on Player 2.
            if (networkManager.IsServer)
                GUILayout.Label($"Oyuncu: {networkManager.ConnectedClientsIds.Count}/{maxPlayers}");
            if (State == SessionState.Connected && GUILayout.Button("Oyuna don (Esc)", GUILayout.Height(30)))
                SetCursor(true);
            if (State != SessionState.Disconnecting && GUILayout.Button("Ayril / Iptal", GUILayout.Height(30)))
                Disconnect();
        }
        GUILayout.EndArea();
    }
}
