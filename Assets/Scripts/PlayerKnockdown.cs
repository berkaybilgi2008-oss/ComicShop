using System.Collections.Generic;
using UnityEngine;

// Presentation and owner-side movement; NetworkPlayerSetup owns multiplayer state.
[DisallowMultipleComponent]
public class PlayerKnockdown : MonoBehaviour
{
    public static readonly HashSet<PlayerKnockdown> Players = new HashSet<PlayerKnockdown>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlayers() => Players.Clear();
    public bool IsDown { get; private set; }
    private NetworkPlayerSetup network;
    private CharacterController capsule;
    private PlayerController movement;
    private Transform view, visual;
    private Vector3 viewPosition, visualPosition, push;
    private Quaternion visualRotation;
    private double readyAt;
    private bool headHit;
    private float verticalSpeed;
    private HeadHitKnockdownAnimation headHitAnimation;
    private bool Local => network == null || !network.IsSpawned || network.IsOwner;
    private double Clock => network != null && network.IsSpawned
        ? network.NetworkManager.ServerTime.Time : Time.timeAsDouble;

    void Awake()
    {
        network = GetComponent<NetworkPlayerSetup>();
        capsule = GetComponent<CharacterController>();
        movement = GetComponent<PlayerController>();
        headHitAnimation = GetComponent<HeadHitKnockdownAnimation>();
        if (headHitAnimation == null) headHitAnimation = gameObject.AddComponent<HeadHitKnockdownAnimation>();
        Camera camera = GetComponentInChildren<Camera>(true);
        view = camera != null ? camera.transform : null;
        if (view != null) viewPosition = view.localPosition;
        visual = transform.Find("Visual");
        if (visual == null)
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                Transform candidate = renderer.transform;
                while (candidate.parent != null && candidate.parent != transform) candidate = candidate.parent;
                if (candidate != transform && (view == null || !view.IsChildOf(candidate)))
                { visual = candidate; break; }
            }
        if (visual != null)
        { visualPosition = visual.localPosition; visualRotation = visual.localRotation; }
    }
    void OnEnable() => Players.Add(this);
    void OnDisable()
    {
        Players.Remove(this);
        SetState(-1);
        if (visual != null) visual.SetLocalPositionAndRotation(visualPosition, visualRotation);
        if (view != null) view.localPosition = viewPosition;
    }

    public Bounds HitBounds
    {
        get
        {
            Vector3 center = capsule != null ? capsule.center : Vector3.up;
            float height = capsule != null ? capsule.height : 2f;
            float radius = capsule != null ? capsule.radius : 0.3f;
            Vector3 scale = transform.lossyScale;
            return new Bounds(transform.TransformPoint(center), new Vector3(
                radius * 2f * Mathf.Abs(scale.x), height * Mathf.Abs(scale.y), radius * 2f * Mathf.Abs(scale.z)));
        }
    }

    public bool IsHeadPoint(Vector3 point)
    {
        return point.y >= HitBounds.max.y - HitBounds.size.y * 0.25f;
    }

    public void Hit(Vector3 velocity, bool head)
    {
        if (IsDown) return;
        Vector3 impulse = Vector3.ProjectOnPlane(velocity, Vector3.up).normalized * (head ? 7f : 4f);
        if (network != null && network.IsSpawned)
        {
            if (network.IsServer) network.KnockDown(impulse, head);
            return;
        }
        ShopAudio.Play(ShopCue.Bonk, transform.position);
        SetState(Clock + (head ? 3d : 0.6d), head);
        Kick(impulse);
    }
    public void SetState(double until, bool head = false)
    {
        headHit = head;
        bool wasDown = IsDown;
        if (headHitAnimation != null) headHitAnimation.SetState(until >= 0, head);
        readyAt = until;
        IsDown = until >= 0;
        if (IsDown && !wasDown)
        {
            verticalSpeed = head ? 3.5f : 1.5f;
            if (head)
            {
                float age = Mathf.Max(0f, (float)(Clock - (until - 3d)));
                if (age < 2f) ComicImpactEffect.Play(HitBounds.center + Vector3.up * HitBounds.extents.y, age);
            }
            var interaction = GetComponent<PlayerInteraction>();
            if (Local && interaction != null) interaction.CancelForKnockdown();
        }
        if (!IsDown) { push = Vector3.zero; verticalSpeed = 0; }
    }
    public void Kick(Vector3 impulse) { if (Local) push = impulse; }

    void Update()
    {
        if (!IsDown || !Local) return;
        if (capsule != null && capsule.enabled)
        {
            if (capsule.isGrounded && verticalSpeed < 0) verticalSpeed = -2f;
            verticalSpeed += (movement != null ? movement.gravity : -20f) * Time.deltaTime;
            capsule.Move((push + Vector3.up * verticalSpeed) * Time.deltaTime);
            push = Vector3.MoveTowards(push, Vector3.zero, 8f * Time.deltaTime);
        }
        if (Clock >= readyAt && Cursor.lockState == CursorLockMode.Locked && Input.GetKeyDown(ShopSettings.Key(ShopAction.Jump)))
        {
            if (network != null && network.IsSpawned) network.StandUpRpc();
            else SetState(-1);
        }
    }
    void LateUpdate()
    {
        float blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
        if (visual != null && !(headHit && headHitAnimation != null && headHitAnimation.IsAnimating))
            visual.localRotation = Quaternion.Slerp(visual.localRotation,
                IsDown ? Quaternion.Euler(0, 0, 80) * visualRotation : visualRotation, blend);
        if (Local && view != null)
            view.localPosition = Vector3.Lerp(view.localPosition,
                viewPosition + (IsDown ? Vector3.down * 0.65f : Vector3.zero), blend);
    }
    void OnGUI()
    {
        if (!IsDown || !Local) return;
        double remaining = readyAt - Clock;
        string label = headHit && remaining > 0
            ? Loc.T("knock.out", Mathf.CeilToInt((float)remaining))
            : remaining > 0 ? Loc.T("knock.down") : Loc.T("knock.getup", ShopHud.KeyName(ShopSettings.Key(ShopAction.Jump)));
        GUI.Box(new Rect(Screen.width / 2f - 180, Screen.height * 0.65f, 360, 45), label);
    }
}
