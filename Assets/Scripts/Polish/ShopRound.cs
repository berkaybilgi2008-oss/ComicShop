using Unity.Netcode;
using UnityEngine;

public struct ShopRoundState : INetworkSerializable, System.IEquatable<ShopRoundState>
{
    public bool Active, Completed;
    public double StartedAt, FinishedAt;
    public int Total;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Active); serializer.SerializeValue(ref Completed);
        serializer.SerializeValue(ref StartedAt); serializer.SerializeValue(ref FinishedAt); serializer.SerializeValue(ref Total);
    }
    public bool Equals(ShopRoundState other) => Active == other.Active && Completed == other.Completed &&
        StartedAt == other.StartedAt && FinishedAt == other.FinishedAt && Total == other.Total;
}

public static class ShopRound
{
    public static ShopRoundState State { get; private set; }
    public static int Revision { get; private set; }
    public static double Clock => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
        ? NetworkManager.Singleton.ServerTime.Time : Time.unscaledTimeAsDouble;
    public static double Elapsed => State.Active ? System.Math.Max(0, (State.Completed ? State.FinishedAt : Clock) - State.StartedAt) : 0;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { State = default; Revision = 0; }
    public static void Reset() { State = default; Revision++; }
    public static void BeginOffline() => Begin();
    public static void Begin()
    {
        State = new ShopRoundState { Active = true, StartedAt = Clock, Total = GameStats.TotalBooks };
        Revision++;
    }
    public static bool ShouldComplete(int total, int placed) => total > 0 && placed == total;
    public static void TickAuthority()
    {
        var manager = NetworkManager.Singleton;
        if (manager != null && (!manager.IsListening || !manager.IsServer)) return;
        var state = State;
        if (!state.Active || state.Completed || !ShouldComplete(state.Total, GameStats.TotalPlaced)) return;
        state.Completed = true; state.FinishedAt = Clock; Apply(state);
        // Completion is latched: taking a book back afterwards never replays rewards/results.
    }
    public static void Apply(ShopRoundState next)
    {
        if (State.Equals(next)) return;
        bool celebrate = next.Completed && !State.Completed;
        State = next; Revision++;
        if (celebrate) ShopAudio.Play(ShopCue.Complete, Vector3.zero, false);
    }
}
