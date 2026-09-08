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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => LocalPlayer = null;

    private void Awake()
    {
        interaction = GetComponent<PlayerInteraction>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
        if (audioListener == null) audioListener = GetComponentInChildren<AudioListener>(true);
        // Awake runs before network ownership is assigned. No prefab may read input yet.
        SetLocal(false);
    }

    public override void OnNetworkSpawn()
    {
        SetLocal(IsOwner);
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
        if (playerCamera == null || float.IsNaN(machinePosition.sqrMagnitude) ||
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
