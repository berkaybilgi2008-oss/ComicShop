using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public class ConnectionManager : MonoBehaviour
{
    public enum SessionState { Idle, PreparingOnline, StartingHost, Connecting, Connected, Disconnecting }
    public enum ConnectionRoute { InternetRelay, DirectIp }

    public static ConnectionManager Instance { get; private set; }
    [Header("Sahne Dogus Noktasi")]
    [Tooltip("Atanirsa oyuncular bu sahne noktasinda dogar. Bos birakilirsa Player Prefab konumu kullanilir.")]
    public Transform playerSpawnPoint;
    [Header("Oturum")]
    public ConnectionRoute connectionRoute = ConnectionRoute.InternetRelay;
    public string address = "127.0.0.1";
    public ushort port = 7777;
    public string relayJoinCode = "";
    public bool showDebugUI = true;
    [Min(1f)] public float connectionTimeout = 15f;
    [Min(5f)] public float onlineServiceTimeout = 30f;
    [Range(1, 4)] public int maxPlayers = 4;
    public SessionState State { get; private set; }
    public bool IsRunning => State != SessionState.Idle || TransportBusy;
    public event Action<string> OnStatusChanged;

    private NetworkManager networkManager;
    private string status = "Bagli degil";
    private float attemptStarted;
    private int onlineOperation;
    private readonly IOnlineSessionTransport relayTransport = new UnityRelaySessionTransport();
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

    public bool StartHost()
    {
        connectionRoute = ConnectionRoute.DirectIp;
        return StartSession(true);
    }

    public bool StartClient()
    {
        connectionRoute = ConnectionRoute.DirectIp;
        return StartSession(false);
    }

    public async void StartRelayHost()
    {
        connectionRoute = ConnectionRoute.InternetRelay;
        if (!BeginOnlinePreparation("Internet odasi hazirlaniyor...")) return;
        int operation = onlineOperation;
        try
        {
            await relayTransport.PrepareHostAsync(networkManager, Mathf.Clamp(maxPlayers, 1, 4));
            if (operation != onlineOperation || State != SessionState.PreparingOnline) return;
            relayJoinCode = relayTransport.JoinCode;
            StartPreparedSession(true, $"Internet odasi kuruluyor. Kod: {relayJoinCode}");
        }
        catch (Exception exception)
        {
            FailOnlinePreparation(operation, exception);
        }
    }

    public async void StartRelayClient()
    {
        connectionRoute = ConnectionRoute.InternetRelay;
        relayJoinCode = UnityRelaySessionTransport.NormalizeJoinCode(relayJoinCode);
        if (relayJoinCode.Length == 0)
        {
            SetStatus("Oda kodunu gir.");
            return;
        }
        if (!BeginOnlinePreparation("Oda kodu kontrol ediliyor...")) return;
        int operation = onlineOperation;
        try
        {
            await relayTransport.PrepareClientAsync(networkManager, relayJoinCode);
            if (operation != onlineOperation || State != SessionState.PreparingOnline) return;
            StartPreparedSession(false, $"Internet odasina baglaniliyor: {relayJoinCode}");
        }
        catch (Exception exception)
        {
            FailOnlinePreparation(operation, exception);
        }
    }

    private bool BeginOnlinePreparation(string message)
    {
        if (!ValidateStart()) return false;
        onlineOperation++;
        State = SessionState.PreparingOnline;
        attemptStarted = Time.realtimeSinceStartup;
        SetStatus(message);
        return true;
    }

    private void FailOnlinePreparation(int operation, Exception exception)
    {
        if (operation != onlineOperation) return;
        Debug.LogException(exception);
        relayTransport.Reset();
        State = SessionState.Idle;
        SetCursor(false);
        SetStatus("Internet odasi hatasi: " + FriendlyOnlineError(exception));
    }

    private static string FriendlyOnlineError(Exception exception)
    {
        string message = exception.Message ?? string.Empty;
        if (message.IndexOf("join", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("allocation", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Oda kodu gecersiz veya odanin suresi dolmus.";
        return message.Length > 140 ? message.Substring(0, 140) : message;
    }

    private bool StartSession(bool host)
    {
        if (!ValidateStart()) return false;
        var transport = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
        if (transport == null)
        {
            SetStatus("NetworkManager: UnityTransport eksik.");
            return false;
        }
        if (!host && string.IsNullOrWhiteSpace(address))
        {
            SetStatus("Baglanilacak adresi gir.");
            return false;
        }
        try
        {
            // Remote address and server listen address are distinct UTP settings.
            transport.SetConnectionData(host ? "127.0.0.1" : address.Trim(), port,
                host ? "0.0.0.0" : null);
            return StartPreparedSession(host,
                host ? $"Yerel oda kuruluyor (port {port})" : $"Yerel aga baglaniliyor: {address}:{port}");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            StopWithStatus($"Baglanti hatasi: {exception.Message}");
            return false;
        }
    }

    private bool ValidateStart()
    {
        if (networkManager == null || IsRunning)
        {
            SetStatus("Once mevcut baglantinin kapanmasini bekle.");
            return false;
        }
        if (networkManager.NetworkConfig.PlayerPrefab == null)
        {
            SetStatus("NetworkManager Player Prefab eksik.");
            return false;
        }
        return true;
    }

    private bool StartPreparedSession(bool host, string startingMessage)
    {
        try
        {
            // Synchronized gameplay layout. Both peers must run this build generation.
            networkManager.NetworkConfig.ProtocolVersion = 3;
            ShelfSlot.BuildNetworkRegistry();
            foreach (var spawner in FindObjectsByType<BookSpawner>(FindObjectsSortMode.None))
                spawner.PrepareSession(networkManager);
            foreach (var machine in FindObjectsByType<BookRecallMachine>(FindObjectsSortMode.None))
                machine.ResetSession();

            networkManager.NetworkConfig.ConnectionApproval = true;
            State = host ? SessionState.StartingHost : SessionState.Connecting;
            attemptStarted = Time.realtimeSinceStartup;
            SetStatus(startingMessage);
            bool started = host ? networkManager.StartHost() : networkManager.StartClient();
            if (!started)
                StopWithStatus(host ? "Oda baslatilamadi." : "Baglanti baslatilamadi.");
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
        onlineOperation++;
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
            // NGO has finished despawning here. Never clear a live session's slots.
            ShelfSlot.ResetNetworkSession();
            GameStats.Initialize(0, 1);
            State = SessionState.Idle;
            SetCursor(false);
            SetStatus(status + " Yeni oturum acabilirsin.");
        }
        float timeout = State == SessionState.PreparingOnline ? onlineServiceTimeout : connectionTimeout;
        if ((State == SessionState.PreparingOnline || State == SessionState.Connecting ||
             State == SessionState.StartingHost) && Time.realtimeSinceStartup - attemptStarted >= timeout)
            StopWithStatus(connectionRoute == ConnectionRoute.InternetRelay
                ? "Internet odasi zaman asimina ugradi. Baglantini ve oda kodunu kontrol et."
                : "Baglanti zaman asimina ugradi. Host ve adresi kontrol et.");
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
        Transform spawn = playerSpawnPoint != null ? playerSpawnPoint : networkManager.NetworkConfig.PlayerPrefab.transform;
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
            if (connectionRoute == ConnectionRoute.InternetRelay)
                SetStatus(networkManager.IsHost ? $"Internet odasi acik. Kod: {relayJoinCode}" : "Internet odasina katildin.");
            else
                SetStatus(networkManager.IsHost ? $"Yerel oda acik (port {port})" : "Yerel odaya katildin.");
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

    private void HandleTransportFailure() => StopWithStatus(connectionRoute == ConnectionRoute.InternetRelay
        ? "Relay ag hatasi. Internet baglantini kontrol et."
        : $"Ag hatasi. Adres ve port {port} ayarini kontrol et.");

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
        GUILayout.BeginArea(new Rect(12, 12, 340, 330), GUI.skin.box);
        GUILayout.Label(status, new GUIStyle(GUI.skin.label) { wordWrap = true });
        if (!IsRunning)
        {
            connectionRoute = (ConnectionRoute)GUILayout.SelectionGrid((int)connectionRoute,
                new[] { "Internet / Oda Kodu", "Yerel Ag / IP" }, 2);
            if (connectionRoute == ConnectionRoute.InternetRelay)
            {
                if (GUILayout.Button("Internet Odasi Kur", GUILayout.Height(32))) StartRelayHost();
                GUILayout.Label("Oda kodu:");
                relayJoinCode = GUILayout.TextField(relayJoinCode, 16).ToUpperInvariant();
                if (GUILayout.Button("Kodla Katil", GUILayout.Height(32))) StartRelayClient();
                GUILayout.Label("Kodu diger oyuncuya gonder. IP veya port acmak gerekmez.",
                    new GUIStyle(GUI.skin.label) { wordWrap = true });
            }
            else
            {
                if (GUILayout.Button("Yerel Oda Kur (Host)", GUILayout.Height(30))) StartHost();
                GUILayout.Label("Host adresi:");
                address = GUILayout.TextField(address);
                if (GUILayout.Button("Yerel Aga Katil", GUILayout.Height(30))) StartClient();
            }
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
