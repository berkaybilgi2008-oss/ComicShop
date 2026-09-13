using UnityEngine;

// Generic rig two-bone correction, applied after animation. No leg/root changes.
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class ToastBookCarry : MonoBehaviour
{
    public Animator animator;
    public Transform upperArm, forearm, hand, bookSocket;
    public bool carryingBook;
    [Tooltip("Offset in model metres. Uses the visual root's uniform scale.")]
    [Range(-0.10f,0.16f)] public float handHeight;
    [Min(0.01f)] public float transitionSeconds=0.15f;
    public GameObject previewBook;
    PlayerInteraction inventory;
    NetworkPlayerSetup networkPlayer;
    [Header("Q Throw Grip")]
    public Transform leftUpperArm, leftForearm, leftHand;
    public Vector3 throwPalmOffset = new Vector3(0f, -0.06f, 0.025f);
    [Range(15f, 60f)] public float minimumThrowElevation = 35f;
    [Range(0.65f, 0.95f)] public float throwReachFraction = 0.88f;
    [Range(25f, 100f)] public float maximumWristBend = 65f;
    [Min(0.25f)] public float throwCameraDistance = 0.5f;
    private ThrowPoseControls poseControls;
    public bool UsesManualThrowPose
    {
        get
        {
            if (!poseControls) poseControls = GetComponentInParent<ThrowPoseControls>();
            return poseControls && poseControls.IsManual;
        }
    }
    public bool TryGetManualThrowPose(BookItem book, bool left, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero; rotation = Quaternion.identity;
        return UsesManualThrowPose && SelectThrowArm(left, out var a, out var b, out var c) &&
            poseControls.GetBookPose(book, a, b, c, out position, out rotation);
    }
    private Quaternion leftWristRest, rightWristRest;
    private Transform throwUpper, throwLower, throwHand;
    private Quaternion throwUpperBase, throwLowerBase, throwHandBase;
    private Quaternion lastUpperPose, lastLowerPose, lastHandPose;
    private bool throwApplied;
    private float throwBlend;
    private BookItem gripBook;
    private Vector3 gripLocal, gripNormal, gripLong;
    float blend;
    bool applied;
    Quaternion upperBase, lowerBase, handBase;
    static readonly int Carry=Animator.StringToHash("Carry");

    void Awake()
    {
        inventory = GetComponentInParent<PlayerInteraction>();
        networkPlayer = GetComponentInParent<NetworkPlayerSetup>();
        foreach (var bone in GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == "LeftUpperArm" && !leftUpperArm) leftUpperArm = bone;
            if (bone.name == "LeftForearm" && !leftForearm) leftForearm = bone;
            if (bone.name == "LeftHand" && !leftHand) leftHand = bone;
        }
        leftWristRest = leftHand ? leftHand.localRotation : Quaternion.identity;
        rightWristRest = hand ? hand.localRotation : Quaternion.identity;
        // Procedural arms can leave the bounds baked into the idle clip. Keep
        // skinning/bounds updated when the torso itself is outside the camera.
        foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skin.updateWhenOffscreen = true;
    }

    void ReadInventory()
    {
        // Visuals can be instantiated before being parented under Player.
        if (!inventory)
        {
            inventory = GetComponentInParent<PlayerInteraction>();
            networkPlayer = GetComponentInParent<NetworkPlayerSetup>();
        }
        if (!inventory) return; // Standalone model previews keep their manual toggle.
        if (networkPlayer && networkPlayer.IsSpawned && !networkPlayer.IsOwner)
            carryingBook = NetworkBook.CountHeldBy(networkPlayer.OwnerClientId) >
                (NetworkBook.FindThrowingBook(networkPlayer.OwnerClientId, out _) != null ? 1 : 0);
        else
            carryingBook = inventory.HeldBooksList.Count > (inventory.IsThrowPoseActive ? 1 : 0);
    }

    void Update()
    {
        RestoreThrow();
        Restore();
        ReadInventory();
        if (!animator) return;
        blend=Mathf.MoveTowards(blend,carryingBook?1f:0f,Time.deltaTime/Mathf.Max(.01f,transitionSeconds));
        animator.SetFloat(Carry,blend);
        if (previewBook) previewBook.SetActive(carryingBook && blend>.8f);
    }
    void LateUpdate()
    {
        ApplyCarryPose();
        ApplyThrowArm();
    }

    void ApplyCarryPose()
    {
        // ThrowArc can finish after Update. Clear the Carry parameter that same
        // frame, independent of socket binding, and skip the old hand correction.
        ReadInventory();
        if (inventory && !carryingBook)
        {
            Restore();
            blend = 0f;
            if (animator) animator.SetFloat(Carry, 0f);
            if (previewBook) previewBook.SetActive(false);
            return;
        }
        if (!animator || !animator.enabled || !upperArm || !forearm || !hand || blend<=0f) return;
        float scale=Mathf.Abs(transform.lossyScale.y);
        float offset=Mathf.Clamp(handHeight,-.10f,.16f)*scale*blend;
        if (Mathf.Abs(offset)<.00001f) return;
        upperBase=upperArm.localRotation;lowerBase=forearm.localRotation;handBase=hand.localRotation;
        Quaternion wristRotation=hand.rotation;
        Vector3 a=upperArm.position,b=forearm.position,c=hand.position;
        Vector3 target=c+transform.up*offset;
        float l1=Vector3.Distance(a,b),l2=Vector3.Distance(b,c);
        if(l1<.0001f||l2<.0001f) return;
        Vector3 delta=target-a;float distance=delta.magnitude;
        if(distance<.0001f)return;
        Vector3 direction=delta/distance;
        float reach=Mathf.Clamp(distance,Mathf.Abs(l1-l2)+.0001f,l1+l2-.0001f);
        target=a+direction*reach;
        Vector3 bend=Vector3.ProjectOnPlane(b-a,direction);
        if(bend.sqrMagnitude<.0000001f) bend=Vector3.ProjectOnPlane(-transform.forward,direction);
        if(bend.sqrMagnitude<.0000001f) bend=Vector3.ProjectOnPlane(transform.right,direction);
        bend.Normalize();
        float along=(l1*l1+reach*reach-l2*l2)/(2f*reach);
        float across=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
        Vector3 elbow=a+direction*along+bend*across;
        upperArm.rotation=Quaternion.FromToRotation(b-a,elbow-a)*upperArm.rotation;
        forearm.rotation=Quaternion.FromToRotation(hand.position-forearm.position,target-forearm.position)*forearm.rotation;
        hand.rotation=wristRotation;
        applied=true;
    }
    private bool SelectThrowArm(bool left, out Transform a, out Transform b, out Transform c)
    {
        a = left ? leftUpperArm : upperArm;
        b = left ? leftForearm : forearm;
        c = left ? leftHand : hand;
        return a && b && c;
    }

    private void GetGrip(BookItem book, Vector3 position, Quaternion rotation, Vector3 scale,
        Transform wrist, out Vector3 target, out Quaternion wristRotation)
    {
        if (gripBook != book)
        {
            gripBook = book;
            gripLocal = Vector3.zero;
            gripNormal = Vector3.forward;
            gripLong = Vector3.up;
            var box = book.GetComponentInChildren<BoxCollider>();
            if (box != null)
            {
                Vector3 dimensions = Vector3.Scale(box.size, box.transform.lossyScale);
                int thin = 0, longest = 0;
                for (int i = 1; i < 3; i++)
                {
                    if (Mathf.Abs(dimensions[i]) < Mathf.Abs(dimensions[thin])) thin = i;
                    if (Mathf.Abs(dimensions[i]) > Mathf.Abs(dimensions[longest])) longest = i;
                }
                if (longest == thin) longest = (thin + 1) % 3;
                int wide = 3 - thin - longest;
                Vector3 normal = Vector3.zero, along = Vector3.zero, across = Vector3.zero;
                normal[thin] = 1f; along[longest] = 1f; across[wide] = 1f;
                Vector3 rootAcross = book.transform.InverseTransformDirection(box.transform.TransformDirection(across));
                Vector3 screenInward = wrist == leftHand ? transform.right : -transform.right;
                if (Vector3.Dot(rotation * rootAcross, screenInward) < 0f) across = -across;
                // Grip the inward-facing cover. A fixed positive normal made
                // some book prefabs turn the wrist through half a revolution.
                Vector3 rootNormal = book.transform.InverseTransformDirection(box.transform.TransformDirection(normal));
                Vector3 inward = wrist == leftHand ? transform.right : -transform.right;
                if (Vector3.Dot(rotation * rootNormal, inward) < 0f) normal = -normal;
                // Grip the lower corner facing the middle of the screen, rather
                // than the center of the edge hidden behind the book.
                Vector3 point = box.center + normal * box.size[thin] * 0.5f
                    - along * box.size[longest] * 0.46f + across * box.size[wide] * 0.43f;
                gripLocal = book.transform.InverseTransformPoint(box.transform.TransformPoint(point));
                gripNormal = book.transform.InverseTransformDirection(box.transform.TransformDirection(normal));
                gripLong = book.transform.InverseTransformDirection(box.transform.TransformDirection(along));
            }
        }
        Vector3 normalWorld = (rotation * gripNormal).normalized;
        Vector3 alongWorld = (rotation * gripLong).normalized;
        wristRotation = Quaternion.LookRotation(-normalWorld, -alongWorld);
        Vector3 palm = position + rotation * Vector3.Scale(scale, gripLocal);
        target = palm - wristRotation * Vector3.Scale(throwPalmOffset, wrist.lossyScale);
    }

    public Vector3 ConstrainThrowReach(BookItem book, Vector3 position, Quaternion rotation, Vector3 scale, bool left)
    {
        if (!SelectThrowArm(left, out var a, out var b, out var c)) return position;
        GetGrip(book, position, rotation, scale, c, out var target, out _);
        float length = Vector3.Distance(a.position, b.position) + Vector3.Distance(b.position, c.position);
        Vector3 up = transform.up;
        Vector3 delta = target - a.position;
        Vector3 horizontal = Vector3.ProjectOnPlane(delta, up);
        float minimumHeight = Mathf.Max(length * 0.38f,
            horizontal.magnitude * Mathf.Tan(minimumThrowElevation * Mathf.Deg2Rad));
        float raise = Mathf.Max(0f, minimumHeight - Vector3.Dot(delta, up));
        // The existing charge entry already interpolates the book; blend in the
        // height floor as well so the first Q frame does not snap upward.
        float poseBlend = Mathf.Clamp01(throwBlend + Time.deltaTime / Mathf.Max(0.01f, transitionSeconds));
        delta += up * raise * poseBlend;
        Vector3 reachable = a.position + Vector3.ClampMagnitude(delta, length * throwReachFraction);
        var camera = inventory != null ? inventory.playerCamera : null;
        if (camera != null && (networkPlayer == null || !networkPlayer.IsSpawned || networkPlayer.IsOwner))
        {
            // Keep a real world-space distance from the lens. Viewport clamping
            // previously pulled the wrist onto the near plane and magnified it.
            Vector3 forward = camera.transform.forward;
            float radius = length * throwReachFraction;
            float shoulderDepth = Vector3.Dot(a.position - camera.transform.position, forward);
            float desiredDepth = Mathf.Max(throwCameraDistance, camera.nearClipPlane + 0.2f);
            // Intersect the reachable sphere with a depth plane, preserving bone
            // lengths instead of repeatedly pulling the pose back toward the lens.
            float safeDepth = Mathf.Min(desiredDepth, shoulderDepth + radius - 0.01f);
            float currentDepth = Vector3.Dot(reachable - camera.transform.position, forward);
            if (currentDepth < safeDepth)
            {
                float offset = safeDepth - shoulderDepth;
                Vector3 center = a.position + forward * offset;
                float lateralRadius = Mathf.Sqrt(Mathf.Max(0f, radius * radius - offset * offset));
                Vector3 lateral = Vector3.ProjectOnPlane(reachable - center, forward);
                reachable = center + Vector3.ClampMagnitude(lateral, lateralRadius);
            }
        }
        return position + reachable - target;
    }

    private void ApplyThrowArm()
    {
        if (!animator || !animator.enabled || !inventory) return;
        bool left = inventory.throwHand == PlayerInteraction.ThrowHand.Left;
        BookItem book = inventory.IsThrowPoseActive ? inventory.ActiveHeldBook : null;
        if (networkPlayer && networkPlayer.IsSpawned && !networkPlayer.IsOwner)
            book = NetworkBook.FindThrowingBook(networkPlayer.OwnerClientId, out left);
        throwBlend = Mathf.MoveTowards(throwBlend, book != null ? 1f : 0f,
            Time.deltaTime / Mathf.Max(0.01f, transitionSeconds));
        if (throwBlend <= 0f) return;
        if (book != null)
        {
            if (!SelectThrowArm(left, out throwUpper, out throwLower, out throwHand)) return;
        }
        if (!throwUpper || !throwLower || !throwHand) return;
        throwUpperBase = throwUpper.localRotation;
        throwLowerBase = throwLower.localRotation;
        throwHandBase = throwHand.localRotation;
        if (book != null)
        {
            if (UsesManualThrowPose)
            {
                poseControls.GetWristFromBook(book, out var manualTarget, out var manualRotation);
                if (!SolveThrowElbow(manualTarget, left)) return;
                throwHand.rotation = manualRotation;
            }
            else
            {
                GetGrip(book, book.transform.position, book.transform.rotation, book.transform.lossyScale,
                    throwHand, out var target, out var wristRotation);
                if (!SolveThrowElbow(target, left)) return;
                Quaternion rest = left ? leftWristRest : rightWristRest;
                Quaternion supportedWrist = Quaternion.RotateTowards(throwLower.rotation * rest,
                    wristRotation, maximumWristBend);
                Vector3 palmOffset = Vector3.Scale(throwPalmOffset, throwHand.lossyScale);
                target += wristRotation * palmOffset - supportedWrist * palmOffset;
                if (!SolveThrowElbow(target, left)) return;
                throwHand.rotation = supportedWrist;
            }
            lastUpperPose = throwUpper.localRotation;
            lastLowerPose = throwLower.localRotation;
            lastHandPose = throwHand.localRotation;
        }
        throwUpper.localRotation = Quaternion.Slerp(throwUpperBase,lastUpperPose,throwBlend);
        throwLower.localRotation = Quaternion.Slerp(throwLowerBase,lastLowerPose,throwBlend);
        throwHand.localRotation = Quaternion.Slerp(throwHandBase,lastHandPose,throwBlend);
        throwApplied = true;
    }

    private bool SolveThrowElbow(Vector3 target, bool left)
    {
        Vector3 a = throwUpper.position, b = throwLower.position, c = throwHand.position;
        float l1 = Vector3.Distance(a,b), l2 = Vector3.Distance(b,c);
        if (l1 < 0.0001f || l2 < 0.0001f || (target-a).sqrMagnitude < 0.000001f) return false;
        Vector3 direction = (target-a).normalized;
        float reach = Mathf.Clamp(Vector3.Distance(a,target), Mathf.Abs(l1-l2)+0.0001f, l1+l2-0.0001f);
        target = a + direction * reach;
        // Keep the elbow below the wrist, with a small outward bias. The old
        // backward pole could leave the forearm horizontal or bend the wrist back.
        Vector3 pole = -transform.up + (left ? -transform.right : transform.right) * 0.25f - transform.forward * 0.15f;
        if (UsesManualThrowPose) pole = poseControls.elbowTarget.position - a;
        Vector3 bend = Vector3.ProjectOnPlane(pole, direction);
        if (bend.sqrMagnitude < 0.000001f) bend = Vector3.ProjectOnPlane(-transform.forward, direction);
        float along = (l1*l1+reach*reach-l2*l2)/(2f*reach);
        Vector3 elbow = a + direction*along + bend.normalized*Mathf.Sqrt(Mathf.Max(0f,l1*l1-along*along));
        throwUpper.rotation = Quaternion.FromToRotation(b-a,elbow-a)*throwUpper.rotation;
        throwLower.rotation = Quaternion.FromToRotation(throwHand.position-throwLower.position,target-throwLower.position)*throwLower.rotation;
        return true;
    }

    private void RestoreThrow()
    {
        if (!throwApplied) return;
        if (throwUpper) throwUpper.localRotation = throwUpperBase;
        if (throwLower) throwLower.localRotation = throwLowerBase;
        if (throwHand) throwHand.localRotation = throwHandBase;
        throwApplied = false;
    }

    void Restore()
    {
        if(!applied)return;
        if(upperArm)upperArm.localRotation=upperBase;
        if(forearm)forearm.localRotation=lowerBase;
        if(hand)hand.localRotation=handBase;
        applied=false;
    }
    void OnDisable() { RestoreThrow(); throwBlend=0f; Restore(); if(animator)animator.SetFloat(Carry,0);blend=0; if(previewBook)previewBook.SetActive(false); }
    public void SetCarrying(bool value) { carryingBook=value; }
}
