using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>One server-owned book. Clients request actions; only the host changes inventory/physics.</summary>
[RequireComponent(typeof(NetworkObject), typeof(BookItem))]
public class NetworkBook : NetworkBehaviour
{
    public const ulong NoHolder = ulong.MaxValue;
    public struct BookState : INetworkSerializable, IEquatable<BookState>
    {
        public int BookId, BrandId, SlotIndex;
        public ulong Holder, Slot;
        public Vector3 Position, Scale;
        public Quaternion Rotation;
        public bool Kinematic;
        public float PlacementDuration;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref BookId);
            serializer.SerializeValue(ref BrandId);
            serializer.SerializeValue(ref Holder);
            serializer.SerializeValue(ref Slot);
            serializer.SerializeValue(ref SlotIndex);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Scale);
            serializer.SerializeValue(ref Kinematic);
            serializer.SerializeValue(ref PlacementDuration);
        }
        public bool Equals(BookState other) => BookId == other.BookId && BrandId == other.BrandId &&
            Holder == other.Holder && Slot == other.Slot && SlotIndex == other.SlotIndex &&
            Position.Equals(other.Position) && Rotation.Equals(other.Rotation) &&
            Scale.Equals(other.Scale) && Kinematic == other.Kinematic && PlacementDuration.Equals(other.PlacementDuration);
    }

    private readonly NetworkVariable<BookState> state = new NetworkVariable<BookState>(
        new BookState { Holder = NoHolder, Rotation = Quaternion.identity, Scale = Vector3.one, SlotIndex = -1 });
    private BookItem item;
    private Rigidbody body;
    private PlayerInteraction boundPlayer;
    private bool hasState;
    private float nextSync;
    private float nextHeldPose;
    private Vector3 placementStartPosition, placementStartScale;
    private Quaternion placementStartRotation;
    private AnimationCurve placementCurve;
    private float placementElapsed;
    public bool IsPlacementAnimating { get; private set; }
    public ulong Holder => state.Value.Holder;
    public bool HeldByLocal => IsSpawned && Holder != NoHolder && Holder == NetworkManager.LocalClientId;

    private void Awake()
    {
        item = GetComponent<BookItem>();
        body = GetComponent<Rigidbody>();
    }

    public void Initialize(int bookId, int brandId)
    {
        state.Value = new BookState
        {
            BookId = bookId, BrandId = brandId, Holder = NoHolder, SlotIndex = -1,
            Position = transform.position, Rotation = transform.rotation, Scale = transform.localScale,
            Kinematic = body != null && body.isKinematic
        };
    }

    public override void OnNetworkSpawn()
    {
        state.OnValueChanged += ApplyState;
        if (!IsServer) BookToonEffect.ApplyToBook(gameObject);
        ApplyState(default, state.Value);
    }

    public override void OnNetworkDespawn()
    {
        IsPlacementAnimating = false;
        state.OnValueChanged -= ApplyState;
        if (boundPlayer != null) boundPlayer.ForgetNetworkBook(item);
        boundPlayer = null;
        // Do not let a local hand destroy this root network object with its parent.
        transform.SetParent(null, true);
        if (item.currentSlot != null) item.currentSlot.RemoveBook(item);
    }

    private void ApplyState(BookState previous, BookState current)
    {
        // The slot is committed immediately by the host; the visible book still travels
        // from its current hand pose. Late joiners receive the final pose without replay.
        bool animatePlacement = hasState && current.Slot != 0 && previous.Slot != current.Slot && current.PlacementDuration > 0f;
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 startScale = transform.lossyScale;
        if (previous.Holder != current.Holder || previous.Slot != current.Slot)
            IsPlacementAnimating = false;
        item.bookID = current.BookId;
        item.brandID = current.BrandId;
        bool changedHolder = item.IsHeld != (current.Holder != NoHolder) || previous.Holder != current.Holder;
        if (item.currentSlot != null && item.currentSlot.NetworkKey != current.Slot)
            item.currentSlot.RemoveBook(item);
        if (boundPlayer != null && !HeldByLocal)
        {
            boundPlayer.ForgetNetworkBook(item);
            boundPlayer = null;
        }
        if (!HeldByLocal) transform.SetParent(null, true);
        if (changedHolder || !IsServer) item.SetHeld(current.Holder != NoHolder);
        if (body != null) body.isKinematic = !IsServer || current.Holder != NoHolder || current.Kinematic;
        if (current.Slot != 0 && item.currentSlot == null)
        {
            var slot = ShelfSlot.FindNetworkSlot(current.Slot);
            if (slot != null) slot.ApplyNetworkPlacement(item, current.SlotIndex);
        }
        if (animatePlacement)
        {
            placementStartPosition = startPosition;
            placementStartRotation = startRotation;
            placementStartScale = startScale;
            placementElapsed = 0f;
            placementCurve = null;
            foreach (var player in FindObjectsByType<NetworkPlayerSetup>(FindObjectsSortMode.None))
            {
                if (player.OwnerClientId != previous.Holder) continue;
                var interaction = player.GetComponent<PlayerInteraction>();
                if (interaction != null) placementCurve = interaction.bookMoveCurve;
                break;
            }
            transform.SetPositionAndRotation(startPosition, startRotation);
            transform.localScale = startScale;
            IsPlacementAnimating = true;
        }
        else if (!HeldByLocal && (!hasState || changedHolder || previous.Slot != current.Slot))
        {
            transform.SetPositionAndRotation(current.Position, current.Rotation);
            transform.localScale = current.Scale;
        }
        hasState = true;
        BindLocalHand();
    }

    private void BindLocalHand()
    {
        if (!HeldByLocal || boundPlayer != null || NetworkPlayerSetup.LocalPlayer == null) return;
        boundPlayer = NetworkPlayerSetup.LocalPlayer.GetComponent<PlayerInteraction>();
        if (boundPlayer != null) boundPlayer.AcceptNetworkBook(item);
    }

    private void LateUpdate()
    {
        if (!IsSpawned) return;
        BindLocalHand();
        if (IsPlacementAnimating)
        {
            var target = state.Value;
            placementElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(placementElapsed / Mathf.Max(0.01f, target.PlacementDuration));
            float t = placementCurve != null ? placementCurve.Evaluate(progress) : Mathf.SmoothStep(0f, 1f, progress);
            transform.position = Vector3.LerpUnclamped(placementStartPosition, target.Position, t);
            transform.rotation = Quaternion.SlerpUnclamped(placementStartRotation, target.Rotation, t);
            transform.localScale = Vector3.LerpUnclamped(placementStartScale, target.Scale, t);
            if (progress >= 1f)
            {
                transform.SetPositionAndRotation(target.Position, target.Rotation);
                transform.localScale = target.Scale;
                IsPlacementAnimating = false;
            }
            // Neither the host's pose publisher nor client snapshot smoothing may
            // overwrite this tween or publish an intermediate pose as the target.
            return;
        }
        if (!IsServer && !HeldByLocal)
        {
            var target = state.Value;
            float blend = target.Slot != 0 ? 1f : 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, target.Position, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, target.Rotation, blend);
            transform.localScale = Vector3.Lerp(transform.localScale, target.Scale, blend);
        }
        if (IsServer && Time.unscaledTime >= nextSync)
        {
            nextSync = Time.unscaledTime + 1f / 15f;
            PublishPose();
        }
        if (HeldByLocal && !IsServer && Time.unscaledTime >= nextHeldPose)
        {
            nextHeldPose = Time.unscaledTime + 1f / 15f;
            HeldPoseRpc(transform.position, transform.rotation, transform.lossyScale);
        }
    }

    private void PublishPose()
    {
        var value = state.Value;
        value.Position = transform.position;
        value.Rotation = transform.rotation;
        value.Scale = transform.lossyScale;
        value.Kinematic = body != null && body.isKinematic;
        if (!value.Equals(state.Value)) state.Value = value;
    }

    private PlayerInteraction GetPlayer(ulong id)
    {
        if (!IsServer || !NetworkManager.ConnectedClients.TryGetValue(id, out var client) || client.PlayerObject == null)
            return null;
        return client.PlayerObject.GetComponent<PlayerInteraction>();
    }

    private bool WithinReach(PlayerInteraction player)
    {
        if (player == null) return false;
        Vector3 eye = player.playerCamera != null ? player.playerCamera.transform.position : player.transform.position;
        var collider = GetComponentInChildren<Collider>();
        Vector3 closest = collider != null ? collider.ClosestPoint(eye) : transform.position;
        return Vector3.Distance(eye, closest) <= player.interactRange + 0.5f;
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void PickUpRpc(RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        var player = GetPlayer(sender);
        if (Holder != NoHolder || IsPlacementAnimating || !WithinReach(player)) return;
        int count = 0;
        foreach (var book in FindObjectsByType<NetworkBook>(FindObjectsSortMode.None))
            if (book.IsSpawned && book.Holder == sender) count++;
        if (count >= player.maxHeldBooks) return;
        if (item.currentSlot != null) item.currentSlot.RemoveBook(item);
        var flight = GetComponent<ThrownBook>();
        if (flight != null) { flight.enabled = false; Destroy(flight); }
        var value = state.Value;
        value.Holder = sender;
        value.Slot = 0;
        value.SlotIndex = -1;
        value.Kinematic = true;
        value.PlacementDuration = 0f;
        state.Value = value; // Atomic claim: a second requester now sees an occupied book.
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void PlaceRpc(ulong slotKey, RpcParams rpc = default)
    {
        var player = GetPlayer(rpc.Receive.SenderClientId);
        var slot = ShelfSlot.FindNetworkSlot(slotKey);
        if (player == null || Holder != rpc.Receive.SenderClientId || slot == null) return;
        Vector3 eye = player.playerCamera != null ? player.playerCamera.transform.position : player.transform.position;
        var collider = slot.GetComponentInChildren<Collider>();
        if (collider == null || Vector3.Distance(eye, collider.ClosestPoint(eye)) > player.interactRange + 0.5f) return;
        if (!slot.TryGetNextPlacementPose(item, out Vector3 position, out _) ||
            Vector3.Distance(player.transform.position, position) > player.maxPlacementDistance) return;
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 startScale = transform.lossyScale;
        // Claim the exact slot/index before the visual animation starts, so concurrent
        // placements cannot reserve the same space or bypass brand/capacity checks.
        if (!slot.PlaceBook(item)) return;
        var value = state.Value;
        value.Holder = NoHolder;
        value.Slot = slotKey;
        value.SlotIndex = slot.GetBookIndex(item);
        value.Position = transform.position;
        value.Rotation = transform.rotation;
        value.Scale = transform.localScale;
        value.Kinematic = true;
        value.PlacementDuration = Mathf.Clamp(player.bookMoveDuration, 0.01f, 5f);
        transform.SetPositionAndRotation(startPosition, startRotation);
        transform.localScale = startScale;
        state.Value = value;
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    public void ReleaseRpc(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 spinAxis,
        float spin, bool charged, RpcParams rpc = default)
    {
        var player = GetPlayer(rpc.Receive.SenderClientId);
        if (player == null || Holder != rpc.Receive.SenderClientId || !ValidPose(player, position, rotation) ||
            !Finite(velocity) || !Finite(spinAxis) || float.IsNaN(spin) || float.IsInfinity(spin)) return;
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(position, rotation);
        float maxSpeed = Mathf.Max(player.maxThrowSpeed * player.releaseSnap,
            player.dropForwardForce + player.dropUpwardForce);
        Release(Vector3.ClampMagnitude(velocity, maxSpeed), spinAxis,
            Mathf.Clamp(spin, -player.maxThrowSpin, player.maxThrowSpin), charged && player.throwAbilityUnlocked);
    }

    private void Release(Vector3 velocity, Vector3 axis, float spin, bool charged)
    {
        var throwingPlayer = GetPlayer(Holder);
        transform.SetParent(null, true);
        transform.localScale = item.OriginalScale;
        var value = state.Value;
        value.Holder = NoHolder;
        value.Slot = 0;
        value.SlotIndex = -1;
        value.Position = transform.position;
        value.Rotation = transform.rotation;
        value.Scale = item.OriginalScale;
        value.Kinematic = false;
        value.PlacementDuration = 0f;
        state.Value = value;
        item.SetHeld(false);
        if (body == null) return;
        body.isKinematic = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = velocity;
        body.angularVelocity = charged ? axis.normalized * spin : Vector3.zero;
        body.WakeUp();
        if (charged)
        {
            var flight = GetComponent<ThrownBook>();
            if (flight == null) flight = gameObject.AddComponent<ThrownBook>();
            flight.Configure(axis, throwingPlayer != null ? throwingPlayer.transform : null);
        }
    }

    [Rpc(SendTo.Server, RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
    private void HeldPoseRpc(Vector3 position, Quaternion rotation, Vector3 scale, RpcParams rpc = default)
    {
        var player = GetPlayer(rpc.Receive.SenderClientId);
        if (Holder != rpc.Receive.SenderClientId || !ValidPose(player, position, rotation) || !Finite(scale)) return;
        transform.SetPositionAndRotation(position, rotation);
        float factor = Mathf.Max(1f, player.heldScaleMultiplier, player.chargeScaleMultiplier);
        transform.localScale = Vector3.Min(Vector3.Max(scale, item.OriginalScale * 0.1f), item.OriginalScale * factor);
    }

    private static bool ValidPose(PlayerInteraction player, Vector3 position, Quaternion rotation)
    {
        float norm = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
        return player != null && Finite(position) && norm > 0.9f && norm < 1.1f &&
            Vector3.Distance(player.transform.position, position) <= 5f;
    }
    private static bool Finite(Vector3 v) => !float.IsNaN(v.sqrMagnitude) && !float.IsInfinity(v.sqrMagnitude);

    public static void ReleaseAllForPlayer(ulong id)
    {
        foreach (var book in FindObjectsByType<NetworkBook>(FindObjectsSortMode.None))
            if (book.IsSpawned && book.IsServer && book.Holder == id)
                book.Release(Vector3.zero, Vector3.zero, 0f, false);
    }
}
