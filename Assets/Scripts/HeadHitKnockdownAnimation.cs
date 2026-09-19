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
    const float FallDuration = 0.72f;
    const float RecoverDuration = 0.26f;

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

        // A readable cartoon sequence:
        // 0-.10  : head-hit recoil
        // .10-.38 : slow backward tip
        // .38-.55 : feet kick high
        // .55-.72 : legs come down + back lands
        float recoil = EaseOut(Mathf.InverseLerp(0f, 0.10f, t));
        float lean = EaseOut(Mathf.InverseLerp(0.06f, 0.38f, t));
        float kickUp = EaseOut(Mathf.InverseLerp(0.22f, 0.48f, t));
        float kickDown = 1f - EaseInOut(Mathf.InverseLerp(0.48f, 0.72f, t));
        float legKick = Mathf.Clamp01(kickUp * kickDown);
        float land = EaseInOut(Mathf.InverseLerp(0.50f, 0.72f, t));

        if (reverse)
        {
            recoil = 1f - recoil;
            lean = 1f - lean;
            legKick = 1f - legKick;
            land = 1f - land;
        }

        // Backward fall: hips/chest lean together so the character falls onto his back,
        // rather than folding forward like a banana peel slip.
        Rotate("Hips", Quaternion.Euler(Mathf.Lerp(0f, 58f, lean), 0f, 0f));
        Rotate("Spine", Quaternion.Euler(Mathf.Lerp(0f, 22f, lean), 0f, 0f));
        Rotate("Chest", Quaternion.Euler(Mathf.Lerp(0f, 30f, lean), 0f, 0f));
        Rotate("Neck", Quaternion.Euler(Mathf.Lerp(0f, -12f, lean), 0f, 0f));
        Rotate("Head", Quaternion.Euler(Mathf.Lerp(0f, -8f, lean), 0f, 0f));

        // Arms: broad cartoon "WHOA!" reaction, not a stiff ragdoll.
        Rotate("LeftUpperArm", Quaternion.Euler(Mathf.Lerp(0f, 12f, recoil), 0f, Mathf.Lerp(0f, -62f, recoil)));
        Rotate("LeftForearm", Quaternion.Euler(Mathf.Lerp(0f, -6f, recoil), 0f, Mathf.Lerp(0f, -16f, recoil)));
        Rotate("RightUpperArm", Quaternion.Euler(Mathf.Lerp(0f, 12f, recoil), 0f, Mathf.Lerp(0f, 62f, recoil)));
        Rotate("RightForearm", Quaternion.Euler(Mathf.Lerp(0f, -6f, recoil), 0f, Mathf.Lerp(0f, 16f, recoil)));

        // Feet point almost straight upward at the comic peak, then visibly fall back down
        // before the back reaches the floor. This prevents the "feet frozen in the air" look.
        float thighAngle = Mathf.Lerp(0f, -102f, legKick);
        float shinAngle = Mathf.Lerp(0f, 30f, legKick);
        float footAngle = Mathf.Lerp(0f, -16f, legKick);
        Rotate("LeftThigh", Quaternion.Euler(thighAngle, 0f, 0f));
        Rotate("RightThigh", Quaternion.Euler(thighAngle, 0f, 0f));
        Rotate("LeftShin", Quaternion.Euler(shinAngle, 0f, 0f));
        Rotate("RightShin", Quaternion.Euler(shinAngle, 0f, 0f));
        Rotate("LeftFoot", Quaternion.Euler(footAngle, 0f, 0f));
        Rotate("RightFoot", Quaternion.Euler(footAngle, 0f, 0f));

        // Final landing: lower the hips and let the whole body settle onto its back.
        if (hips != null && basePose.TryGetValue("Hips", out Pose hipsPose))
        {
            Vector3 p = hipsPose.position;
            float drop = Mathf.SmoothStep(0f, 0.58f, land);
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
