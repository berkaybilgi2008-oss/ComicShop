using Unity.Netcode;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class NetworkPlayerSetup : NetworkBehaviour
{
    public static NetworkPlayerSetup LocalPlayer { get; private set; }
    public Camera playerCamera;
    public AudioListener audioListener;
    public MonoBehaviour[] ownerOnlyComponents;
    public bool connectHudToLocalPlayer = true;
    private PlayerInteraction interaction;
    private PlayerKnockdown knockdown;
    private float nextPowerRequest;
    public bool IsDown => downState.Value.ReadyAt >= 0;
    public struct DownState : INetworkSerializable, System.IEquatable<DownState>
    {
        public double ReadyAt;
        public bool Head;
        public Vector3 Impulse;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ReadyAt);
            serializer.SerializeValue(ref Head);
            serializer.SerializeValue(ref Impulse);
        }
        public bool Equals(DownState other) => ReadyAt == other.ReadyAt && Head == other.Head && Impulse == other.Impulse;
    }
    private readonly NetworkVariable<DownState> downState = new NetworkVariable<DownState>(new DownState { ReadyAt = -1 });

    public void KnockDown(Vector3 impulse, bool head)
    {
        if (!IsServer || IsDown || float.IsNaN(impulse.sqrMagnitude) || float.IsInfinity(impulse.sqrMagnitude)) return;
        downState.Value = new DownState { ReadyAt = NetworkManager.ServerTime.Time + (head ? 3d : 0.6d),
            Head = head, Impulse = Vector3.ClampMagnitude(impulse, 8f) };
    }
    private void ApplyDownState(DownState before, DownState after)
    {
        knockdown.SetState(after.ReadyAt, after.Head);
        if (IsOwner && before.ReadyAt < 0 && after.ReadyAt >= 0) knockdown.Kick(after.Impulse);
    }
    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void StandUpRpc()
    {
        if (IsDown && NetworkManager.ServerTime.Time >= downState.Value.ReadyAt)
            downState.Value = new DownState { ReadyAt = -1 };
    }

    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void YaratikGucuRpc(int bookId)
    {
        var power = GetComponent<YaratikGucu>();
        if (IsDown || power == null || Time.unscaledTime < nextPowerRequest ||
            (power.sadeceEditorVeDevBuild && !Application.isEditor && !Debug.isDebugBuild)) return;
        nextPowerRequest = Time.unscaledTime + 1f;
        var books = FindObjectsByType<NetworkBook>(FindObjectsSortMode.None);
        bool ownsType = false;
        int held = 0;
        foreach (var book in books)
        {
            if (!book.IsSpawned || book.Holder != OwnerClientId) continue;
            held++;
            if (book.GetComponent<BookItem>().bookID == bookId) ownsType = true;
        }
        if (!ownsType || interaction == null) return;
        System.Array.Sort(books, (a, b) => (a.transform.position - transform.position).sqrMagnitude
            .CompareTo((b.transform.position - transform.position).sqrMagnitude));
        int recalled = 0;
        foreach (var book in books)
        {
            var item = book.GetComponent<BookItem>();
            if (!book.IsSpawned || book.Holder != NetworkBook.NoHolder || book.IsPlacementAnimating ||
                item.bookID != bookId || (!power.raftakileriDeAl && book.SlotKey != 0)) continue;
            if (power.maksAdet > 0 && recalled >= power.maksAdet) break;
            bool take = power.mod == YaratikGucu.Mod.ElimeGetir && held < interaction.maxHeldBooks;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float angle = recalled * 137.5f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Mathf.Clamp(power.dokmeYaricapi, 0, 2);
            Vector3 point = transform.position + forward * Mathf.Clamp(power.dokmeMesafesi, 0.5f, 3f) +
                Vector3.up * Mathf.Clamp(power.dokmeYuksekligi, 0.1f, 2f) + offset;
            if (!book.RecallForPlayer(OwnerClientId, point, take)) continue;
            recalled++;
            if (take) held++;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => LocalPlayer = null;

    private void Awake()
    {
        interaction = GetComponent<PlayerInteraction>();
        knockdown = GetComponent<PlayerKnockdown>();
        if (knockdown == null) knockdown = gameObject.AddComponent<PlayerKnockdown>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
        if (audioListener == null) audioListener = GetComponentInChildren<AudioListener>(true);
        // Awake runs before network ownership is assigned. No prefab may read input yet.
        SetLocal(false);
    }

    public override void OnNetworkSpawn()
    {
        downState.OnValueChanged += ApplyDownState;
        SetLocal(IsOwner);
        knockdown.SetState(downState.Value.ReadyAt, downState.Value.Head);
        gameObject.name = IsOwner ? $"Player_LOCAL_{OwnerClientId}" : $"Player_{OwnerClientId}";
        if (!IsOwner) return;
        LocalPlayer = this;
        if (interaction != null)
        {
            interaction.playerCamera = playerCamera;
            InteractionSettingsSanitizer.Apply(interaction);
            if (GetComponent<HeldBookHandOffset>() == null) gameObject.AddComponent<HeldBookHandOffset>();
            if (GetComponent<HeldBookVisualSpacing>() == null) gameObject.AddComponent<HeldBookVisualSpacing>();
        }
        if (connectHudToLocalPlayer)
        {
            var hud = FindFirstObjectByType<GameHUD>();
            if (hud != null) hud.playerInteraction = interaction;
        }
        ConnectionManager.SetCursor(true);
    }

    public override void OnNetworkDespawn()
    {
        downState.OnValueChanged -= ApplyDownState;
        knockdown.SetState(-1);
        // Books remain server-owned and must survive the departing player's destruction.
        if (IsServer) NetworkBook.ReleaseAllForPlayer(OwnerClientId);
        if (interaction != null) interaction.ResetInteraction();
        SetLocal(false);
        if (LocalPlayer != this) return;
        LocalPlayer = null;
        var hud = FindFirstObjectByType<GameHUD>();
        if (hud != null && hud.playerInteraction == interaction) hud.playerInteraction = null;
        ConnectionManager.SetCursor(false);
    }

    [Rpc(SendTo.Server, RequireOwnership = true)]
    public void RecallRpc(Vector3 machinePosition, Vector3 lookDirection)
    {
        float magnitude = lookDirection.sqrMagnitude;
        if (IsDown || playerCamera == null || float.IsNaN(machinePosition.sqrMagnitude) ||
            float.IsInfinity(machinePosition.sqrMagnitude) || float.IsNaN(magnitude) ||
            magnitude < 0.9f || magnitude > 1.1f) return;
        foreach (var machine in FindObjectsByType<BookRecallMachine>(FindObjectsSortMode.None))
        {
            if ((machine.transform.position - machinePosition).sqrMagnitude > 0.001f) continue;
            Vector3 delta = machine.transform.position - playerCamera.transform.position;
            if (delta.sqrMagnitude > machine.useRange * machine.useRange) return;
            if (machine.requireLookingAt && Vector3.Dot(lookDirection, delta.normalized) < machine.lookThreshold) return;
            if (machine.recallMode == BookRecallMachine.RecallMode.AllLostBooks) machine.TryRecallAllLost();
            else machine.TryRecall(machine.targetBookID);
            return;
        }
    }

    [Rpc(SendTo.Owner)]
    public void RestoreRejectedReleaseRpc(NetworkObjectReference bookReference, RpcParams rpc = default)
    {
        if (rpc.Receive.SenderClientId != Unity.Netcode.NetworkManager.ServerClientId ||
            !IsOwner || interaction == null || !bookReference.TryGet(out NetworkObject bookObject)) return;
        var book = bookObject.GetComponent<NetworkBook>();
        if (book != null && book.HeldByLocal)
            interaction.AcceptNetworkBook(bookObject.GetComponent<BookItem>());
    }

    private void SetLocal(bool local)
    {
        SetEnabled(GetComponent<PlayerController>(), local);
        SetEnabled(GetComponent<PlayerInteraction>(), local);
        SetEnabled(GetComponent<Crosshair>(), local);
        SetEnabled(GetComponent<HeldBookVisualSpacing>(), local);
        SetEnabled(GetComponent<CharacterController>(), local);
        SetEnabled(playerCamera, local);
        if (playerCamera != null) playerCamera.tag = local ? "MainCamera" : "Untagged";
        SetEnabled(audioListener, local);
        if (ownerOnlyComponents != null)
            foreach (var component in ownerOnlyComponents)
                if (component != this) SetEnabled(component, local);
    }

    private static void SetEnabled(Behaviour component, bool value)
    {
        if (component != null) component.enabled = value;
    }
    private static void SetEnabled(CharacterController component, bool value)
    {
        if (component != null) component.enabled = value;
    }
}
