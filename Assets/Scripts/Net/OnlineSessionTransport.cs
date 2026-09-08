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
    private const string ConnectionType = "dtls";

    public string ProviderName => "Unity Relay";
    public string JoinCode { get; private set; } = string.Empty;

    public async Task PrepareHostAsync(NetworkManager manager, int maxPlayers)
    {
        UnityTransport transport = RequireTransport(manager);
        await EnsureSignedInAsync();

        // Relay expects joining peers, excluding the host itself.
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(
            Math.Max(1, maxPlayers - 1));
        JoinCode = (await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId))
            .Trim().ToUpperInvariant();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));
    }

    public async Task PrepareClientAsync(NetworkManager manager, string joinCode)
    {
        UnityTransport transport = RequireTransport(manager);
        await EnsureSignedInAsync();

        string normalized = NormalizeJoinCode(joinCode);
        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(normalized);
        JoinCode = normalized;
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, ConnectionType));
    }

    public void Reset() => JoinCode = string.Empty;

    public static string NormalizeJoinCode(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static UnityTransport RequireTransport(NetworkManager manager)
    {
        if (manager == null || !(manager.NetworkConfig.NetworkTransport is UnityTransport transport))
            throw new InvalidOperationException("NetworkManager uzerinde UnityTransport bulunamadi.");
        return transport;
    }

    private static async Task EnsureSignedInAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }
}
