using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

/// <summary>
/// Store/network-provider boundary. Gameplay only talks to NGO; Relay today and a
/// future Steam transport can implement this contract without changing book/player code.
/// </summary>
public interface IOnlineSessionTransport
{
    string ProviderName { get; }
    string JoinCode { get; }
    Task PrepareHostAsync(NetworkManager manager, int maxPlayers);
    Task PrepareClientAsync(NetworkManager manager, string joinCode);
    void Reset();
}

public sealed class UnityRelaySessionTransport : IOnlineSessionTransport
{
    // UDP is the most compatible desktop transport for Relay. DTLS can be blocked by\n    // some firewalls/NAT setups even when Relay itself is reachable.\n    private const string ConnectionType = "udp";
    private int generation;
    private static Task signInTask;

    private void CheckCurrent(int operation, UnityTransport transport)
    {
        if (operation != generation || transport == null)
            throw new OperationCanceledException("Relay preparation was superseded.");
    }

    public string ProviderName => "Unity Relay";
    public string JoinCode { get; private set; } = string.Empty;

    public async Task PrepareHostAsync(NetworkManager manager, int maxPlayers)
    {
        int operation = ++generation;
        UnityTransport transport = RequireTransport(manager);
        await EnsureSignedInAsync();
        CheckCurrent(operation, transport);

        // Relay expects joining peers, excluding the host itself.
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(
            Math.Max(1, maxPlayers - 1));
        CheckCurrent(operation, transport);
        string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        CheckCurrent(operation, transport);
        JoinCode = code.Trim().ToUpperInvariant();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));
    }

    public async Task PrepareClientAsync(NetworkManager manager, string joinCode)
    {
        int operation = ++generation;
        UnityTransport transport = RequireTransport(manager);
        await EnsureSignedInAsync();
        CheckCurrent(operation, transport);

        string normalized = NormalizeJoinCode(joinCode);
        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(normalized);
        CheckCurrent(operation, transport);
        JoinCode = normalized;
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));
    }

    public void Reset() { generation++; JoinCode = string.Empty; }

    public static string NormalizeJoinCode(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static UnityTransport RequireTransport(NetworkManager manager)
    {
        if (manager == null || !(manager.NetworkConfig.NetworkTransport is UnityTransport transport))
            throw new InvalidOperationException("NetworkManager uzerinde UnityTransport bulunamadi.");
        return transport;
    }

    private static Task EnsureSignedInAsync()
    {
        // A cancelled preparation can still be inside UGS authentication. Share
        // that task instead of starting a second concurrent anonymous sign-in.
        if (signInTask == null || signInTask.IsCompleted) signInTask = SignInCoreAsync();
        return signInTask;
    }

    private static async Task SignInCoreAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }
}
