using UnityEngine;

// Drives only the visual Animator. It never moves Player or reads input.
[DisallowMultipleComponent]
public sealed class ToastLocomotion : MonoBehaviour
{
    public Animator animator;
    [Tooltip("Assign the Player transform moved by your existing controller/network system.")]
    public Transform motionSource;
    public bool automaticSpeed = true;
    [Min(0.01f)] public float walkSpeed = 1.0f;
    [Min(0.02f)] public float runSpeed = 2.5f;
    [Min(0)] public float smoothing = 0.12f;
    PlayerController movement;
    Vector3 previousPosition;
    bool initialized;
    static readonly int Speed = Animator.StringToHash("Speed");

    void Reset() { animator = GetComponent<Animator>(); }
    void OnEnable() { initialized = false; }
    void Update()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!automaticSpeed || !animator) return;
        Transform source = motionSource ? motionSource : transform;
        Vector3 now = source.position;
        if (!initialized) { previousPosition = now; initialized = true; return; }
        float dt = Time.deltaTime;
        Vector3 delta = now - previousPosition;
        previousPosition = now;
        delta.y = 0;
        if (dt <= 0) return;
        // Ignore a teleport rather than briefly triggering a sprint.
        SetSpeed(delta.magnitude > 2f ? 0f : delta.magnitude / dt);
    }

    // Existing movement code can call this when automaticSpeed is disabled.
    public void SetSpeed(float metresPerSecond)
    {
        if (!animator) return;
        if (!movement) movement = GetComponentInParent<PlayerController>();
        // Prefab preview speeds are not the gameplay controller's 4.5/7.5 m/s.
        // Read configured movement speeds for both owners and remote visuals.
        float walk = Mathf.Max(0.01f, movement ? movement.walkSpeed : walkSpeed);
        float run = Mathf.Max(walk + 0.01f, movement ? movement.sprintSpeed : runSpeed);
        float speed = Mathf.Max(0, metresPerSecond);
        float blend = speed <= walk ? speed / walk : 1f + Mathf.InverseLerp(walk, run, speed);
        animator.SetFloat(Speed, blend, smoothing, Time.deltaTime);
    }
}
