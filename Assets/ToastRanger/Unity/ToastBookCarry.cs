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

    [Header("Q Atis Kolu")]
    public Transform leftUpperArm, leftForearm, leftHand;

    // Automatic reference pose; independent of old Inspector tuning values.
    private const float throwWindupAngle = 2f, throwChargedAngle = -10f, throwReleaseAngle = 40f;
    private const float gripAlongFraction = 0.92f, gripAcrossFraction = -0.86f;
    private const float wristBendLimit = 65f;
    private static readonly Vector3 throwGripOffset = new Vector3(0f, -0.06f, 0.025f);
    private static readonly Vector3 wristTwistEuler = Vector3.zero;
    private static readonly Vector3 elbowPoleBias = new Vector3(0f, -1f, 1f);

    private Quaternion leftWristRest, rightWristRest;
    private Transform throwUpper, throwLower, throwHand;
    private Quaternion throwUpperBase, throwLowerBase, throwHandBase;
    private Quaternion lastUpperPose, lastLowerPose, lastHandPose;
    private bool throwApplied;
    private float throwBlend;
    private Vector3 throwWristTarget;
    private Quaternion throwWristRotation = Quaternion.identity;
    private int throwPoseFrame = -1;
    private Vector3 framedUpper, framedLower, framedElbow;
    private bool framingReady;
    private bool framingLeft;
    private float framingTime = -1f;
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

    /// <summary>Kol kemikleri ve kamera hazirsa poz bu scriptten uretilir.</summary>
    public bool HasThrowArm(bool left)
    {
        return SelectThrowArm(left, out _, out _, out _) && inventory && inventory.playerCamera;
    }

    /// <summary>Sarj ve savurma ilerlemesinden kolun yay acisi.</summary>
    public float ThrowSwingAngle(float charge, float release)
    {
        float windup = Mathf.Lerp(throwWindupAngle, throwChargedAngle, Mathf.Clamp01(charge));
        return Mathf.Lerp(windup, throwReleaseAngle, Mathf.Clamp01(release));
    }

    private float ArmLength(Transform a, Transform b, Transform c)
    {
        return Vector3.Distance(a.position, b.position) + Vector3.Distance(b.position, c.position);
    }

    private void GetBookFrame(BookItem book, Quaternion rotation, Vector3 scale,
        out Vector3 cover, out Vector3 along, out Vector3 wide, out Vector3 half)
    {
        book.GetAxisFrame(out Vector3 localCover, out Vector3 localAlong, out Vector3 localWide, out half);
        cover = (rotation * localCover).normalized;
        along = (rotation * localAlong).normalized;
        wide = (rotation * localWide).normalized;
        // Olculer OriginalScale'e gore alindi; sarj sirasinda kitap buyuyebiliyor.
        float reference = book.OriginalScale.magnitude;
        if (reference > 0.0001f) half *= scale.magnitude / reference;
    }

    /// <summary>
    /// Q pozunun tamami: once elin nerede duracagi, sonra kitabin o ele gore yeri.
    /// Kitap eli takip eder -- ters yon (kitap cekip kolu zorlamak) artik yok.
    /// </summary>
    public bool GetThrowPose(BookItem book, float angle, bool left, Vector3 scale,
        out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        if (!book || !SelectThrowArm(left, out var upper, out var lower, out var wrist)) return false;
        Camera camera = inventory ? inventory.playerCamera : null;
        if (!camera) return false;

        Transform lens = camera.transform;
        float armLength = ArmLength(upper, lower, wrist);
        if (armLength < 0.0001f) return false;
        float side = left ? -1f : 1f;
        Vector3 shoulder = upper.position;

        // Follow body heading, with only a small share of camera pitch.
        // Looking down must not pull the raised upper arm down with the lens.
        Vector3 bodyUp = transform.up;
        Vector3 forward = Vector3.ProjectOnPlane(lens.forward, bodyUp);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(transform.forward, bodyUp);
        forward.Normalize();
        Vector3 right = Vector3.Cross(bodyUp, forward).normalized;
        float pitch = Vector3.SignedAngle(forward, lens.forward, right);
        Quaternion tilt = Quaternion.AngleAxis(Mathf.Clamp(pitch * 0.25f, -15f, 15f), right);
        forward = tilt * forward;

        float windup = Mathf.InverseLerp(throwWindupAngle, throwChargedAngle, angle);
        float release = Mathf.InverseLerp(throwWindupAngle, throwReleaseAngle, angle);
        // Upright throughout charging, with the thin edge facing the throw direction.
        // Only the release stroke tips the book forward.
        Vector3 longAxis = Quaternion.AngleAxis(release * 35f, right) * bodyUp;
        Vector3 coverNormal = right * side;
        rotation = book.GetAlignedRotation(coverNormal, longAxis);
        GetBookFrame(book, rotation, scale,
            out Vector3 cover, out Vector3 along, out Vector3 wide, out Vector3 half);
        if (Vector3.Dot(wide, transform.forward) < 0f) wide = -wide;
        Vector3 bookOffset = along * (half.y * gripAlongFraction)
            + wide * (half.z * gripAcrossFraction) - cover * half.x;
        Quaternion grip = Quaternion.LookRotation(-cover, -along) * Quaternion.Euler(wristTwistEuler);

        // Construct the bent arm from its measured bones, rather than placing
        // the wrist near maximum reach (which straightened the elbow).
        float upperLength = Vector3.Distance(upper.position, lower.position);
        float lowerLength = Vector3.Distance(lower.position, wrist.position);
        Vector3 horizontal = Vector3.ProjectOnPlane(forward, bodyUp).normalized;
        // Search reachable bent-arm poses against the actual camera and book size.
        // Keep the biceps raised; change sideways bend instead of pushing the
        // wrist through the lens or lifting the book past the top of the screen.
        float bestScore = float.PositiveInfinity;
        Vector3 bestUpper = horizontal, bestLower = bodyUp;
        Vector3 palmOffset = grip * Vector3.Scale(throwGripOffset, wrist.lossyScale);
        // Bend more tightly (65-degree inner elbow angle) and keep the arm
        // on its own side of the oversized character head.
        float bendRadians = 65f * Mathf.Deg2Rad;
        Vector3 headCenter = lens.position - horizontal * 0.12f;
        float headRadius = Mathf.Max(0.24f, armLength * 0.35f);
        for (int yaw = 15; yaw <= 75; yaw += 15)
        for (int swivel = -75; swivel <= 75; swivel += 15)
        {
            Vector3 heading = Quaternion.AngleAxis(yaw * side, bodyUp) * horizontal;
            Vector3 axis = Vector3.Cross(bodyUp, heading).normalized;
            Vector3 candidateUpper = Quaternion.AngleAxis(-(45f + windup * 10f), axis) * heading;
            Vector3 candidateLower = Vector3.ProjectOnPlane(bodyUp, candidateUpper).normalized;
            candidateLower = Quaternion.AngleAxis(swivel * side, candidateUpper) * candidateLower;
            candidateLower = candidateLower * Mathf.Sin(bendRadians)
                - candidateUpper * Mathf.Cos(bendRadians);
            Vector3 elbow = shoulder + candidateUpper * upperLength;
            Vector3 candidateWrist = elbow + candidateLower * lowerLength;
            Vector3 center = candidateWrist + palmOffset + bookOffset;
            float score = FramePenalty(camera, candidateWrist) + FramePenalty(camera, elbow);
            // Distance from head sphere to the oriented book box, including faces
            // (corner-only tests miss a head intersecting the middle of a cover).
            Vector3 toHead = headCenter - center;
            Vector3 closest = center
                + cover * Mathf.Clamp(Vector3.Dot(toHead, cover), -half.x, half.x)
                + along * Mathf.Clamp(Vector3.Dot(toHead, along), -half.y, half.y)
                + wide * Mathf.Clamp(Vector3.Dot(toHead, wide), -half.z, half.z);
            float overlap = Mathf.Max(0f, headRadius + 0.04f - Vector3.Distance(headCenter, closest));
            float sideExtent = Mathf.Abs(Vector3.Dot(cover, right)) * half.x
                + Mathf.Abs(Vector3.Dot(along, right)) * half.y
                + Mathf.Abs(Vector3.Dot(wide, right)) * half.z;
            float inward = Mathf.Max(0f, headRadius + 0.04f + sideExtent
                - Vector3.Dot(center - headCenter, right * side));
            score += 10000f * (overlap * overlap + inward * inward);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = center + cover * (half.x * ((corner & 1) == 0 ? -1f : 1f))
                    + along * (half.y * ((corner & 2) == 0 ? -1f : 1f))
                    + wide * (half.z * ((corner & 4) == 0 ? -1f : 1f));
                score += FramePenalty(camera, point);
            }
            Vector3 screen = camera.WorldToViewportPoint(center);
            score += 0.01f * ((screen.x - (left ? 0.20f : 0.80f)) *
                (screen.x - (left ? 0.20f : 0.80f)) + (screen.y - 0.58f) * (screen.y - 0.58f));
            if (score < bestScore)
            {
                bestScore = score;
                bestUpper = candidateUpper;
                bestLower = candidateLower;
            }
        }
        if (!framingReady || framingLeft != left || Time.time - framingTime > 0.25f)
        {
            framedUpper = bestUpper;
            framedLower = bestLower;
            framingReady = true;
            framingLeft = left;
        }
        float follow = 1f - Mathf.Exp(-18f * Time.deltaTime);
        framedUpper = Vector3.Slerp(framedUpper, bestUpper, follow).normalized;
        framedLower = Vector3.Slerp(framedLower, bestLower, follow);
        Vector3 bendDirection = Vector3.ProjectOnPlane(framedLower, framedUpper).normalized;
        framedLower = bendDirection * Mathf.Sin(bendRadians)
            - framedUpper * Mathf.Cos(bendRadians);
        framingTime = Time.time;
        Vector3 upperDirection = Vector3.Slerp(framedUpper, horizontal, release);
        Vector3 lowerDirection = Vector3.Slerp(framedLower, upperDirection, release * 0.8f);
        framedElbow = shoulder + upperDirection * upperLength;
        Vector3 wristTarget = framedElbow + lowerDirection * lowerLength;
        Vector3 palm = wristTarget + grip * Vector3.Scale(throwGripOffset, wrist.lossyScale);

        position = palm + bookOffset;
        throwWristTarget = wristTarget;
        throwWristRotation = grip;
        throwPoseFrame = Time.frameCount;
        return true;
    }

    private static float FramePenalty(Camera camera, Vector3 point)
    {
        Vector3 p = camera.WorldToViewportPoint(point);
        if (p.z <= camera.nearClipPlane + 0.05f)
            return 1000f + 100f * Mathf.Abs(p.z - camera.nearClipPlane - 0.05f);
        float x = Mathf.Max(0f, 0.08f - p.x, p.x - 0.92f);
        float y = Mathf.Max(0f, 0.08f - p.y, p.y - 0.90f);
        return 100f * (x * x + y * y);
    }

    /// <summary>Duvar/oda sinirlamasi kitabi kaydirinca el de ayni kadar kayar.</summary>
    public void OffsetThrowWrist(Vector3 offset)
    {
        if (throwPoseFrame == Time.frameCount) throwWristTarget += offset;
    }

    /// <summary>
    /// Uzak oyuncularda sadece kitabin dunya pozu gelir. Ayni tutustan geri
    /// hesaplayarak bilegi buluruz, boylece herkeste ayni gorunur.
    /// </summary>
    private bool DeriveWristFromBook(BookItem book, bool left, out Vector3 wristTarget, out Quaternion wristRotation)
    {
        wristTarget = Vector3.zero;
        wristRotation = Quaternion.identity;
        if (!book || !SelectThrowArm(left, out var upper, out _, out var wrist)) return false;

        Vector3 bookPosition = book.transform.position;
        GetBookFrame(book, book.transform.rotation, book.transform.lossyScale,
            out Vector3 cover, out Vector3 along, out Vector3 wide, out Vector3 half);

        float side = left ? -1f : 1f;
        if (Vector3.Dot(wide, transform.forward) < 0f) wide = -wide;

        Vector3 palm = bookPosition - along * (half.y * gripAlongFraction)
            - wide * (half.z * gripAcrossFraction) + cover * half.x;
        wristRotation = Quaternion.LookRotation(-cover, -along) * Quaternion.Euler(wristTwistEuler);
        wristTarget = palm - wristRotation * Vector3.Scale(throwGripOffset, wrist.lossyScale);
        return true;
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
            Vector3 target;
            Quaternion wristRotation;
            if (!DeriveWristFromBook(book, left, out target, out wristRotation)) return;
            if (!SolveThrowElbow(target, left)) return;
            // Bilek on koldan kopmasin: kitaba dogru donerken sinirli sapma.
            Quaternion rest = left ? leftWristRest : rightWristRest;
            throwHand.rotation = Quaternion.RotateTowards(throwLower.rotation * rest, wristRotation, wristBendLimit);
            // The bend limit changes the palm offset; compensate at the wrist so
            // the fingers remain at the same book corner after limiting rotation.
            Vector3 localGrip = Vector3.Scale(throwGripOffset, throwHand.lossyScale);
            Vector3 palm = target + wristRotation * localGrip;
            Quaternion limitedRotation = throwHand.rotation;
            if (!SolveThrowElbow(palm - limitedRotation * localGrip, left)) return;
            throwHand.rotation = limitedRotation;
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
        // Elbow forward and raised above the shoulder. Together with the
        // measured-bone wrist target this selects the 90-degree charging bend.
        Vector3 outward = left ? -transform.right : transform.right;
        Vector3 pole = Vector3.ProjectOnPlane(
            outward * elbowPoleBias.x + transform.up * elbowPoleBias.y + transform.forward * elbowPoleBias.z,
            direction);
        // Use the selected elbow, so IK cannot choose the opposite (low-biceps)
        // solution for the same wrist. Remote players retain the fallback pole.
        if (throwPoseFrame == Time.frameCount)
            pole = Vector3.ProjectOnPlane(framedElbow - a, direction);
        if (pole.sqrMagnitude < 0.000001f) pole = Vector3.ProjectOnPlane(outward, direction);
        if (pole.sqrMagnitude < 0.000001f) pole = Vector3.ProjectOnPlane(-transform.forward, direction);
        if (pole.sqrMagnitude < 0.000001f) pole = Vector3.ProjectOnPlane(-transform.up, direction);
        if (pole.sqrMagnitude < 0.000001f) return false;
        float along = (l1*l1+reach*reach-l2*l2)/(2f*reach);
        Vector3 elbow = a + direction*along + pole.normalized*Mathf.Sqrt(Mathf.Max(0f,l1*l1-along*along));
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
