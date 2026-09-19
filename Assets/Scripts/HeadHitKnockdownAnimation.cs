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
    const float FallDuration = 0.42f;
    const float RecoverDuration = 0.20f;

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

        // Four compact beats:
        // 0-.08  : cartoon recoil
        // .08-.20: strong backward lean
        // .20-.31: feet kick up
        // .31-.42: back hits floor and body settles
        float recoil = EaseOut(Mathf.InverseLerp(0f, 0.08f, t));
        float lean = EaseOut(Mathf.InverseLerp(0.05f, 0.20f, t));
        float kick = EaseOut(Mathf.InverseLerp(0.15f, 0.31f, t));
        float land = EaseOut(Mathf.InverseLerp(0.28f, 0.42f, t));

        if (reverse)
        {
            // Stand-up animation only interpolates from the final pose back to neutral.
            recoil = 1f - recoil;
            lean = 1f - lean;
            kick = 1f - kick;
            land = 1f - land;
        }

        // The positive X pitch is intentional: this rig's forward direction makes it
        // fall backward rather than face-first.
        Rotate("Hips", Quaternion.Euler(Mathf.Lerp(0f, 72f, lean), 0f, 0f));
        Rotate("Spine", Quaternion.Euler(Mathf.Lerp(0f, 30f, lean), 0f, 0f));
        Rotate("Chest", Quaternion.Euler(Mathf.Lerp(0f, 38f, lean), 0f, 0f));
        Rotate("Neck", Quaternion.Euler(Mathf.Lerp(0f, -18f, lean), 0f, 0f));
        Rotate("Head", Quaternion.Euler(Mathf.Lerp(0f, -12f, lean), 0f, 0f));

        // Arms open out for a readable comic-book "whoa!" reaction.
        Rotate("LeftUpperArm", Quaternion.Euler(Mathf.Lerp(0f, 18f, recoil), 0f, Mathf.Lerp(0f, -72f, recoil)));
        Rotate("LeftForearm", Quaternion.Euler(Mathf.Lerp(0f, -8f, recoil), 0f, Mathf.Lerp(0f, -22f, recoil)));
        Rotate("RightUpperArm", Quaternion.Euler(Mathf.Lerp(0f, 18f, recoil), 0f, Mathf.Lerp(0f, 72f, recoil)));
        Rotate("RightForearm", Quaternion.Euler(Mathf.Lerp(0f, -8f, recoil), 0f, Mathf.Lerp(0f, 22f, recoil)));

        // Feet shoot upward briefly, then return down as the back lands.
        float legKick = kick * (1f - 0.35f * land);
        Rotate("LeftThigh", Quaternion.Euler(Mathf.Lerp(0f, -62f, lean) - 18f * legKick, 0f, 0f));
        Rotate("RightThigh", Quaternion.Euler(Mathf.Lerp(0f, -62f, lean) - 18f * legKick, 0f, 0f));
        Rotate("LeftShin", Quaternion.Euler(Mathf.Lerp(0f, 38f, legKick), 0f, 0f));
        Rotate("RightShin", Quaternion.Euler(Mathf.Lerp(0f, 38f, legKick), 0f, 0f));
        Rotate("LeftFoot", Quaternion.Euler(Mathf.Lerp(0f, -12f, kick) + Mathf.Lerp(0f, 8f, land), 0f, 0f));
        Rotate("RightFoot", Quaternion.Euler(Mathf.Lerp(0f, -12f, kick) + Mathf.Lerp(0f, 8f, land), 0f, 0f));

        // Lower the hips during the final beat so the body actually settles onto the floor.
        // This is restored from the cached rig pose when the animation finishes.
        if (hips != null && basePose.TryGetValue("Hips", out Pose hipsPose))
        {
            Vector3 p = hipsPose.position;
            float drop = Mathf.SmoothStep(0f, 0.78f, land);
            hips.localPosition = p + Vector3.down * drop;
        }
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
