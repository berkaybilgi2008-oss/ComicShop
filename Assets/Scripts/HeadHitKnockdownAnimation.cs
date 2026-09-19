using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cartoon head-hit fall followed by a real physics ragdoll.
/// The character recoils backward, kicks the feet up, hits the floor on the back,
/// then becomes fully limp. Stand-up blends from the actual ragdoll pose back to the rig.
/// </summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class HeadHitKnockdownAnimation : MonoBehaviour
{
    const float FallDuration = 0.75f;
    const float RecoverDuration = 0.42f;

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
    readonly Dictionary<string, Pose> ragdollPose = new Dictionary<string, Pose>();
    readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    readonly Dictionary<string, Rigidbody> bodies = new Dictionary<string, Rigidbody>();
    readonly List<Collider> ragdollColliders = new List<Collider>();
    readonly List<CharacterJoint> ragdollJoints = new List<CharacterJoint>();

    // Hand off to physics as soon as the cartoon fall has reached the back-first,
    // near-horizontal pose. Do not wait for the full settle animation.
    const float RagdollTriggerTime = 0.47f;

    Animator animator;
    Transform hips;
    CharacterController capsule;
    bool active;
    bool recovering;
    bool ragdollActive;
    float timer;
    float recoverTimer;

    public bool IsAnimating => active || recovering || ragdollActive;

    void Awake() => CacheRig();

    void CacheRig()
    {
        if (animator != null) return;

        animator = GetComponentInChildren<Animator>(true);
        if (animator == null) return;

        capsule = GetComponent<CharacterController>();

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

    void BuildRagdoll()
    {
        if (bodies.Count > 0) return;

        string[] ragdollBones =
        {
            "Hips", "Spine", "Chest", "Neck", "Head",
            "LeftUpperArm", "LeftForearm", "LeftHand",
            "RightUpperArm", "RightForearm", "RightHand",
            "LeftThigh", "LeftShin", "LeftFoot",
            "RightThigh", "RightShin", "RightFoot"
        };

        foreach (string name in ragdollBones)
        {
            if (!bones.TryGetValue(name, out Transform bone)) continue;

            Rigidbody body = bone.GetComponent<Rigidbody>();
            if (body == null) body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = name == "Hips" ? 4.0f : name == "Chest" ? 2.5f : 0.85f;
            body.isKinematic = true;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.drag = 0.65f;
            body.angularDrag = 3.5f;
            body.maxAngularVelocity = 7f;
            bodies[name] = body;

            Collider collider = bone.GetComponent<Collider>();
            if (collider == null)
                collider = CreateCollider(name, bone);

            if (collider != null)
            {
                collider.enabled = false;
                ragdollColliders.Add(collider);
            }
        }

        foreach (var pair in bodies)
        {
            Transform bone = bones[pair.Key];
            Transform parent = bone.parent;
            Rigidbody parentBody = null;

            while (parent != null && parentBody == null)
            {
                parentBody = parent.GetComponent<Rigidbody>();
                parent = parent.parent;
            }

            if (parentBody == null) continue;

            CharacterJoint joint = bone.GetComponent<CharacterJoint>();
            if (joint == null) joint = bone.gameObject.AddComponent<CharacterJoint>();
            if (!ragdollJoints.Contains(joint)) ragdollJoints.Add(joint);

            joint.connectedBody = parentBody;
            joint.enablePreprocessing = false;

            SoftJointLimit low = joint.lowTwistLimit;
            low.limit = -45f;
            joint.lowTwistLimit = low;

            SoftJointLimit high = joint.highTwistLimit;
            high.limit = 45f;
            joint.highTwistLimit = high;

            SoftJointLimit swing1 = joint.swing1Limit;
            swing1.limit = 55f;
            joint.swing1Limit = swing1;

            SoftJointLimit swing2 = joint.swing2Limit;
            swing2.limit = 55f;
            joint.swing2Limit = swing2;

            joint.enableCollision = false;
            joint.autoConfigureConnectedAnchor = true;
            joint.connectedAnchor = parentBody.transform.InverseTransformPoint(bone.position);
            joint.anchor = Vector3.zero;
            // CharacterJoint is a Component, not a Behaviour, so it has no enabled property.
            // The joint can stay active while its Rigidbody is kinematic; physics is enabled
            // simply by switching the ragdoll bodies to non-kinematic.
        }
    }

    Collider CreateCollider(string name, Transform bone)
    {
        if (name == "Head")
        {
            SphereCollider sphere = bone.gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.18f;
            return sphere;
        }

        if (name == "Hips" || name == "Spine" || name == "Chest")
        {
            BoxCollider box = bone.gameObject.AddComponent<BoxCollider>();
            box.size = name == "Hips"
                ? new Vector3(0.36f, 0.42f, 0.24f)
                : new Vector3(0.42f, 0.34f, 0.24f);
            return box;
        }

        CapsuleCollider capsuleCollider = bone.gameObject.AddComponent<CapsuleCollider>();
        Vector3 child = FindPrimaryChildOffset(bone);
        float length = Mathf.Max(0.16f, child.magnitude);
        capsuleCollider.direction = 1;
        capsuleCollider.radius = Mathf.Clamp(length * 0.22f, 0.055f, 0.13f);
        capsuleCollider.height = length + capsuleCollider.radius * 2f;
        capsuleCollider.center = child * 0.5f;
        return capsuleCollider;
    }

    static Vector3 FindPrimaryChildOffset(Transform bone)
    {
        if (bone.childCount == 0) return Vector3.up * 0.16f;

        Transform child = bone.GetChild(0);
        if (child.name == "Outline" && bone.childCount > 1)
            child = bone.GetChild(1);

        return child.localPosition.sqrMagnitude > 0.001f
            ? child.localPosition
            : Vector3.up * 0.16f;
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
            if (active || ragdollActive)
                BeginRecover();
            else
                RestoreBasePose();
        }
    }

    void BeginFall()
    {
        if (animator == null || basePose.Count == 0) return;

        StopRagdoll();
        BuildRagdoll();
        active = true;
        recovering = false;
        ragdollActive = false;
        timer = 0f;
        recoverTimer = 0f;

        if (capsule != null) capsule.enabled = false;
        animator.enabled = false;
        SetRagdollBodiesKinematic(true);
        RestoreBasePose();
    }

    void BeginRecover()
    {
        if (animator == null)
        {
            active = false;
            ragdollActive = false;
            return;
        }

        if (ragdollActive)
        {
            CaptureRagdollPose();

            // Move the player root to the actual ragdoll landing position before
            // the stand-up blend, so recovery happens where the body ended up.
            if (hips != null)
            {
                Vector3 delta = hips.position - transform.position;
                delta.y = 0f;
                transform.position += delta;
            }

            SetRagdollBodiesKinematic(true);
            SetRagdollColliders(false);
            ragdollActive = false;
        }

        active = true;
        recovering = true;
        recoverTimer = 0f;
        animator.enabled = false;
    }

    void EnableRagdoll()
    {
        // IMPORTANT: freeze the exact pose we are looking at before physics starts.
        // Without this, enabling CharacterJoints can solve the skeleton for one
        // physics step and visibly snap the character upright.
        foreach (var pair in bodies)
        {
            if (!bones.TryGetValue(pair.Key, out Transform bone)) continue;
            Rigidbody body = pair.Value;
            body.isKinematic = true;
            body.position = bone.position;
            body.rotation = bone.rotation;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        CaptureRagdollPose();
        animator.enabled = false;
        SetRagdollColliders(true);

        // Keep every rigidbody at the captured world pose while physics takes over.
        foreach (var pair in bodies)
        {
            if (!bones.TryGetValue(pair.Key, out Transform bone)) continue;
            Rigidbody body = pair.Value;
            body.position = bone.position;
            body.rotation = bone.rotation;
            body.isKinematic = false;
            body.useGravity = true;
            body.drag = 0.65f;
            body.angularDrag = 3.5f;
            body.maxAngularVelocity = 7f;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        ragdollActive = true;
        active = true;

        // No artificial launch impulse: gravity and the current pose take over naturally.
        // This prevents the ragdoll from shooting/flying away at the handoff.
    }

    void CaptureRagdollPose()
    {
        ragdollPose.Clear();
        foreach (var pair in bones)
            ragdollPose[pair.Key] = new Pose(pair.Value);
    }

    void SetRagdollBodiesKinematic(bool value)
    {
        foreach (Rigidbody body in bodies.Values)
        {
            body.isKinematic = value;
            body.useGravity = !value;
        }
    }

    void SetRagdollColliders(bool value)
    {
        foreach (Collider collider in ragdollColliders)
            if (collider != null)
                collider.enabled = value;
    }

    void StopRagdoll()
    {
        SetRagdollBodiesKinematic(true);
        SetRagdollColliders(false);
        ragdollActive = false;
    }

    void LateUpdate()
    {
        if (!active) return;

        if (recovering)
        {
            recoverTimer += Time.deltaTime;
            float t = Mathf.Clamp01(recoverTimer / RecoverDuration);
            float eased = EaseInOut(t);

            foreach (var pair in basePose)
            {
                if (!bones.TryGetValue(pair.Key, out Transform bone)) continue;
                if (!ragdollPose.TryGetValue(pair.Key, out Pose from)) continue;

                bone.localPosition = Vector3.Lerp(from.position, pair.Value.position, eased);
                bone.localRotation = Quaternion.Slerp(from.rotation, pair.Value.rotation, eased);
            }

            if (t >= 1f)
            {
                recovering = false;
                active = false;
                RestoreBasePose();
                DestroyRagdollComponents();
                animator.enabled = true;
                if (capsule != null) capsule.enabled = true;
            }

            return;
        }

        if (ragdollActive) return;

        timer += Time.deltaTime;
        float tFall = Mathf.Clamp01(timer / FallDuration);
        ApplyPose(tFall);

        if (tFall >= RagdollTriggerTime)
            EnableRagdoll();
    }

    void ApplyPose(float t)
    {
        float hit = EaseOut(Mathf.InverseLerp(0.00f, 0.10f, t));
        float backward = EaseInOut(Mathf.InverseLerp(0.06f, 0.32f, t));
        float airborne = EaseOut(Mathf.InverseLerp(0.22f, 0.45f, t));
        float impact = EaseInOut(Mathf.InverseLerp(0.50f, 0.62f, t));
        float settle = EaseOut(Mathf.InverseLerp(0.62f, 0.75f, t));
        float feetUp = airborne * (1f - EaseInOut(Mathf.InverseLerp(0.45f, 0.75f, t)));

        Rotate("Hips", Quaternion.Euler(Mathf.Lerp(0f, -64f, backward), 0f, 0f));
        Rotate("Spine", Quaternion.Euler(Mathf.Lerp(0f, -26f, backward), 0f, 0f));
        Rotate("Chest", Quaternion.Euler(Mathf.Lerp(0f, -34f, backward), 0f, 0f));
        Rotate("Neck", Quaternion.Euler(Mathf.Lerp(0f, 14f, hit), 0f, 0f));
        Rotate("Head", Quaternion.Euler(Mathf.Lerp(0f, 10f, hit), 0f, 0f));

        Rotate("LeftUpperArm", Quaternion.Euler(Mathf.Lerp(0f, 20f, hit), 0f, Mathf.Lerp(0f, -78f, hit)));
        Rotate("LeftForearm", Quaternion.Euler(Mathf.Lerp(0f, -12f, hit), 0f, Mathf.Lerp(0f, -20f, hit)));
        Rotate("RightUpperArm", Quaternion.Euler(Mathf.Lerp(0f, 20f, hit), 0f, Mathf.Lerp(0f, 78f, hit)));
        Rotate("RightForearm", Quaternion.Euler(Mathf.Lerp(0f, -12f, hit), 0f, Mathf.Lerp(0f, 20f, hit)));

        float thigh = Mathf.Lerp(0f, -96f, feetUp);
        float shin = Mathf.Lerp(0f, 42f, feetUp);
        float foot = Mathf.Lerp(0f, -18f, feetUp);
        Rotate("LeftThigh", Quaternion.Euler(thigh, 0f, 0f));
        Rotate("RightThigh", Quaternion.Euler(thigh, 0f, 0f));
        Rotate("LeftShin", Quaternion.Euler(shin, 0f, 0f));
        Rotate("RightShin", Quaternion.Euler(shin, 0f, 0f));
        Rotate("LeftFoot", Quaternion.Euler(foot, 0f, 0f));
        Rotate("RightFoot", Quaternion.Euler(foot, 0f, 0f));

        if (hips != null && basePose.TryGetValue("Hips", out Pose hipsPose))
        {
            Vector3 p = hipsPose.position;
            // The body should visibly lose height while falling, not only rotate backward.
            // Start lowering before horizontal and continue into the ragdoll handoff.
            float dropProgress = Mathf.Clamp01(Mathf.InverseLerp(0.18f, RagdollTriggerTime, t));
            float drop = Mathf.SmoothStep(0f, 0.48f, dropProgress);
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

    void DestroyRagdollComponents()
    {
        foreach (CharacterJoint joint in ragdollJoints)
            if (joint != null)
                Destroy(joint);

        foreach (Rigidbody body in bodies.Values)
            if (body != null)
                Destroy(body);

        foreach (Collider collider in ragdollColliders)
            if (collider != null)
                Destroy(collider);

        ragdollJoints.Clear();
        bodies.Clear();
        ragdollColliders.Clear();
        ragdollPose.Clear();
    }

    void OnDisable()
    {
        StopRagdoll();
        DestroyRagdollComponents();
        if (animator != null) animator.enabled = true;
    }
}
