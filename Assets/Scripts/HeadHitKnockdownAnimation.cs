using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stylized, physics-like head-hit fall for the ToastRanger character.
/// It directly drives the existing rig, so locomotion and networking do not need to be replaced.
/// </summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class HeadHitKnockdownAnimation : MonoBehaviour
{
    const float FallDuration = 0.92f;
    const float RecoverDuration = 0.24f;

    struct Pose
    {
        public Vector3 position;
        public Quaternion rotation;
        public Pose(Transform t)
        {
            position = t.localPosition;
            rotation = t.localRotation;
        }
    }

    readonly Dictionary<string, Pose> basePose = new Dictionary<string, Pose>();
    readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();

    Animator animator;
    bool active;
    bool recovering;
    float timer;
    float recoverTimer;

    public bool IsAnimating => active;

    void Awake() => CacheRig();

    void CacheRig()
    {
        if (animator != null) return;
        animator = GetComponentInChildren<Animator>(true);
        if (animator == null) return;

        foreach (Transform t in animator.GetComponentsInChildren<Transform>(true))
            if (!bones.ContainsKey(t.name)) bones.Add(t.name, t);

        string[] names =
        {
            "Hips", "Spine", "Chest", "Neck", "Head",
            "LeftUpperArm", "LeftForearm", "LeftHand",
            "RightUpperArm", "RightForearm", "RightHand",
            "LeftThigh", "LeftShin", "LeftFoot",
            "RightThigh", "RightShin", "RightFoot"
        };

        foreach (string name in names)
            if (bones.TryGetValue(name, out Transform bone))
                basePose[name] = new Pose(bone);
    }

    public void SetState(bool down, bool headHit)
    {
        CacheRig();
        if (headHit && down)
        {
            if (!active || recovering) BeginFall();
            return;
        }

        if (!down)
        {
            if (active) BeginRecover();
            else RestoreBasePose();
        }
    }

    void BeginFall()
    {
        if (animator == null || basePose.Count == 0) return;
        active = true;
        recovering = false;
        timer = 0f;
        recoverTimer = 0f;
        animator.enabled = false;
        RestoreBasePose();
    }

    void BeginRecover()
    {
        if (animator == null)
        {
            active = false;
            return;
        }
        recovering = true;
        recoverTimer = 0f;
    }

    void LateUpdate()
    {
        if (!active) return;

        if (recovering)
        {
            recoverTimer += Time.deltaTime;
            float t = Mathf.Clamp01(recoverTimer / RecoverDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            ApplyPose(eased, true);
            if (t >= 1f)
            {
                active = false;
                recovering = false;
                RestoreBasePose();
                animator.enabled = true;
            }
            return;
        }

        timer += Time.deltaTime;
        ApplyPose(Mathf.Clamp01(timer / FallDuration), false);
    }

    void ApplyPose(float normalized, bool reverse)
    {
        float t = Mathf.Clamp01(normalized);
        float fall = t < 0.12f
            ? Mathf.SmoothStep(0f, 0.16f, t / 0.12f)
            : Mathf.SmoothStep(0.16f, 1f, (t - 0.12f) / 0.88f);
        if (reverse) fall = 1f - fall;

        Rotate("Hips", Quaternion.Euler(Mathf.Lerp(0f, -88f, fall), 0f, 0f));
        Rotate("Spine", Quaternion.Euler(Mathf.Lerp(0f, -24f, fall), 0f, 0f));
        Rotate("Chest", Quaternion.Euler(Mathf.Lerp(0f, -34f, fall), 0f, 0f));
        Rotate("Neck", Quaternion.Euler(Mathf.Lerp(0f, 22f, fall), 0f, 0f));
        Rotate("Head", Quaternion.Euler(Mathf.Lerp(0f, 12f, fall), 0f, 0f));

        Rotate("LeftUpperArm", Quaternion.Euler(Mathf.Lerp(0f, -22f, fall), 0f, Mathf.Lerp(0f, -68f, fall)));
        Rotate("LeftForearm", Quaternion.Euler(Mathf.Lerp(0f, -18f, fall), 0f, Mathf.Lerp(0f, -18f, fall)));
        Rotate("RightUpperArm", Quaternion.Euler(Mathf.Lerp(0f, -22f, fall), 0f, Mathf.Lerp(0f, 68f, fall)));
        Rotate("RightForearm", Quaternion.Euler(Mathf.Lerp(0f, -18f, fall), 0f, Mathf.Lerp(0f, 18f, fall)));

        Rotate("LeftThigh", Quaternion.Euler(Mathf.Lerp(0f, -92f, fall), 0f, 0f));
        Rotate("LeftShin", Quaternion.Euler(Mathf.Lerp(0f, 48f, fall), 0f, 0f));
        Rotate("RightThigh", Quaternion.Euler(Mathf.Lerp(0f, -92f, fall), 0f, 0f));
        Rotate("RightShin", Quaternion.Euler(Mathf.Lerp(0f, 48f, fall), 0f, 0f));
        Rotate("LeftFoot", Quaternion.Euler(Mathf.Lerp(0f, -18f, fall), 0f, 0f));
        Rotate("RightFoot", Quaternion.Euler(Mathf.Lerp(0f, -18f, fall), 0f, 0f));
    }

    void Rotate(string name, Quaternion offset)
    {
        if (!bones.TryGetValue(name, out Transform bone) || !basePose.TryGetValue(name, out Pose pose)) return;
        bone.localRotation = pose.rotation * offset;
    }

    void RestoreBasePose()
    {
        foreach (var pair in basePose)
        {
            if (!bones.TryGetValue(pair.Key, out Transform bone)) continue;
            bone.localPosition = pair.Value.position;
            bone.localRotation = pair.Value.rotation;
        }
    }
}
