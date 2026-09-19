using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fast, soft, exaggerated cartoon fall for a head-hit knockdown.
/// The character recoils backward, kicks both feet up, then lands flat on the back.
/// It drives the existing ToastRanger rig and temporarily pauses locomotion while active.
/// </summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class HeadHitKnockdownAnimation : MonoBehaviour
{
    const float FallDuration = 0.75f;
    const float RecoverDuration = 0.30f;

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
    Transform hips;
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
            if (!bones.ContainsKey(t.name))
                bones.Add(t.name, t);

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

        bones.TryGetValue("Hips", out hips);
    }

    public void SetState(bool down, bool headHit)
    {
        CacheRig();

        if (headHit && down)
        {
            BeginFall();
            return;
        }

        if (!down)
        {
            if (active)
                BeginRecover();
            else
                RestoreBasePose();
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

        // Match the requested cartoon storyboard:
        // 0.00 normal
        // 0.05 book/head reaction
        // 0.10 surprised recoil
        // 0.20 backward sway
        // 0.32 airborne loss of balance
        // 0.45 feet high
        // 0.62 back-first impact
        // 0.75 fully sprawled on the back
        float hit = EaseOut(Mathf.InverseLerp(0.00f, 0.10f, t));
        float backward = EaseInOut(Mathf.InverseLerp(0.06f, 0.32f, t));
        float airborne = EaseOut(Mathf.InverseLerp(0.22f, 0.45f, t));
        float impact = EaseInOut(Mathf.InverseLerp(0.50f, 0.62f, t));
        float settle = EaseOut(Mathf.InverseLerp(0.62f, 0.75f, t));

        // Feet are raised during the airborne part, then come down naturally after impact.
        float feetUp = airborne * (1f - EaseInOut(Mathf.InverseLerp(0.45f, 0.75f, t)));

        if (reverse)
        {
            hit = 1f - hit;
            backward = 1f - backward;
            feetUp = 1f - feetUp;
            impact = 1f - impact;
            settle = 1f - settle;
        }

        // Lean backwards. Positive X is the rig's backward fall direction.
        Rotate("Hips", Quaternion.Euler(Mathf.Lerp(0f, 64f, backward), 0f, 0f));
        Rotate("Spine", Quaternion.Euler(Mathf.Lerp(0f, 26f, backward), 0f, 0f));
        Rotate("Chest", Quaternion.Euler(Mathf.Lerp(0f, 34f, backward), 0f, 0f));

        // Head snaps back slightly from the book, then follows the fall.
        Rotate("Neck", Quaternion.Euler(Mathf.Lerp(0f, -14f, hit), 0f, 0f));
        Rotate("Head", Quaternion.Euler(Mathf.Lerp(0f, -10f, hit), 0f, 0f));

        // Exaggerated cartoon arms: open and trail during the fall.
        Rotate("LeftUpperArm", Quaternion.Euler(Mathf.Lerp(0f, 20f, hit), 0f, Mathf.Lerp(0f, -78f, hit)));
        Rotate("LeftForearm", Quaternion.Euler(Mathf.Lerp(0f, -12f, hit), 0f, Mathf.Lerp(0f, -20f, hit)));
        Rotate("RightUpperArm", Quaternion.Euler(Mathf.Lerp(0f, 20f, hit), 0f, Mathf.Lerp(0f, 78f, hit)));
        Rotate("RightForearm", Quaternion.Euler(Mathf.Lerp(0f, -12f, hit), 0f, Mathf.Lerp(0f, 20f, hit)));

        // Legs shoot up like the reference, with soles visible, then fall back toward the floor.
        float thigh = Mathf.Lerp(0f, -112f, feetUp);
        float shin = Mathf.Lerp(0f, 42f, feetUp);
        float foot = Mathf.Lerp(0f, -18f, feetUp);
        Rotate("LeftThigh", Quaternion.Euler(thigh, 0f, 0f));
        Rotate("RightThigh", Quaternion.Euler(thigh, 0f, 0f));
        Rotate("LeftShin", Quaternion.Euler(shin, 0f, 0f));
        Rotate("RightShin", Quaternion.Euler(shin, 0f, 0f));
        Rotate("LeftFoot", Quaternion.Euler(foot, 0f, 0f));
        Rotate("RightFoot", Quaternion.Euler(foot, 0f, 0f));

        // Back-first landing. Do not leave the hips floating: lower them progressively
        // during impact and the final settle.
        if (hips != null && basePose.TryGetValue("Hips", out Pose hipsPose))
        {
            Vector3 p = hipsPose.position;
            float drop = Mathf.SmoothStep(0f, 0.62f, Mathf.Clamp01(impact + settle));
            hips.localPosition = p + Vector3.down * drop;
        }
    }

    static float EaseInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    void Rotate(string name, Quaternion offset)
    {
        if (!bones.TryGetValue(name, out Transform bone) ||
            !basePose.TryGetValue(name, out Pose pose))
            return;

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
