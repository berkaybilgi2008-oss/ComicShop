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
    private static readonly Vector3 elbowPoleBias = new Vector3(0f, 1f, 0.25f);

    private Quaternion leftWristRest, rightWristRest;
    public Quaternion GetThrowWristRest(bool left) => left ? leftWristRest : rightWristRest;
    private Transform throwUpper, throwLower, throwHand;
    private Quaternion throwUpperBase, throwLowerBase, throwHandBase;
    private Quaternion lastUpperPose, lastLowerPose, lastHandPose;
    private bool throwApplied;
    private float throwBlend;
    private Vector3 throwWristTarget;
    private Quaternion throwWristRotation = Quaternion.identity;
    public float VisualCharge { get; private set; }
    public float VisualRelease { get; private set; }
    private int throwPoseFrame = -1;
    private Vector3 framedElbow;
    float nextPoseSend, remotePoseUntil;
    bool wasSendingThrow, remoteLeft;
    Quaternion remoteUpper, remoteLower, remoteHand;
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
        if (!GetComponent<FirstPersonThrowView>())
            gameObject.AddComponent<FirstPersonThrowView>();
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
        if (networkPlayer && networkPlayer.IsSpawned && networkPlayer.IsOwner && inventory)
        {
            bool active = inventory.IsThrowPoseActive;
            if ((active && Time.unscaledTime >= nextPoseSend) || (wasSendingThrow && !active))
            {
                nextPoseSend = Time.unscaledTime + 1f / 30f;
                networkPlayer.SubmitThrowPoseRpc(lastUpperPose, lastLowerPose, lastHandPose,
                    inventory.throwHand == PlayerInteraction.ThrowHand.Left, active);
            }
            wasSendingThrow = active;
        }
    }

    public void ReceiveThrowPose(Quaternion upper, Quaternion lower, Quaternion wrist, bool left, bool active)
    {
        remoteUpper = upper; remoteLower = lower; remoteHand = wrist; remoteLeft = left;
        // The final reliable pose survives book detachment, including a one-frame release.
        remotePoseUntil = Time.unscaledTime + (active ? 0.25f : 0.10f);
    }

    bool ApplyRemoteThrow()
    {
        if (!networkPlayer || !networkPlayer.IsSpawned || networkPlayer.IsOwner) return false;
        bool active = Time.unscaledTime < remotePoseUntil && !networkPlayer.IsDown;
        throwBlend = Mathf.MoveTowards(throwBlend, active ? 1f : 0f, Time.deltaTime / 0.08f);
        if (throwBlend <= 0f || !SelectThrowArm(remoteLeft, out throwUpper, out throwLower, out throwHand)) return true;
        throwUpperBase = throwUpper.localRotation;
        throwLowerBase = throwLower.localRotation;
        throwHandBase = throwHand.localRotation;
        throwUpper.localRotation = Quaternion.Slerp(throwUpperBase, remoteUpper, throwBlend);
        throwLower.localRotation = Quaternion.Slerp(throwLowerBase, remoteLower, throwBlend);
        throwHand.localRotation = Quaternion.Slerp(throwHandBase, remoteHand, throwBlend);
        throwApplied = true;
        return true;
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
        // Pull the book back 45 degrees during windup, then snap forward on release.
        Vector3 longAxis = Quaternion.AngleAxis(Mathf.Lerp(-45f, 35f, release), right) * bodyUp;
        Vector3 coverNormal = -right * side;
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
        // World pose is intentionally more extended than the owner-only view.
        // No camera-framing search: every observer receives the same book pose.
        VisualCharge = windup;
        VisualRelease = release;
        Vector3 outwardHeading = Quaternion.AngleAxis(side * 55f, bodyUp) * horizontal;
        Vector3 shoulderAxis = Vector3.Cross(bodyUp, outwardHeading).normalized;
        Quaternion shoulderLift = Quaternion.AngleAxis(
            -Mathf.Lerp(30f + windup * 10f, 5f, release), shoulderAxis);
        Vector3 upperDirection = shoulderLift * outwardHeading;
        // 115 degrees inside the elbow, with the upper arm abducted away from the head.
        float bend = 115f * Mathf.Deg2Rad;
        // Keep the forearm forward rather than folding behind the head.
        // Keep the forearm in the torso's forward/up plane while retaining
        // the requested inner elbow angle (rather than splaying it sideways).
        float uy = Vector3.Dot(upperDirection, bodyUp);
        float uz = Vector3.Dot(upperDirection, horizontal);
        float planeLength = Mathf.Sqrt(uy * uy + uz * uz);
        float forearmAngle = Mathf.Atan2(uy, uz) + Mathf.Acos(
            Mathf.Clamp(-Mathf.Cos(bend) / Mathf.Max(0.0001f, planeLength), -1f, 1f));
        Vector3 lowerDirection = bodyUp * Mathf.Sin(forearmAngle)
            + horizontal * Mathf.Cos(forearmAngle);
        // Slight left lean around the upper-arm axis preserves elbow angle/length.
        lowerDirection = Quaternion.AngleAxis(4f, upperDirection) * lowerDirection;
        lowerDirection = Vector3.Slerp(lowerDirection, upperDirection, release * 0.95f);
        framedElbow = shoulder + upperDirection * upperLength;
        Vector3 wristTarget = framedElbow + lowerDirection * lowerLength;
        Vector3 palm = wristTarget + grip * Vector3.Scale(throwGripOffset, wrist.lossyScale);

        position = palm + bookOffset;
        throwWristTarget = wristTarget;
        throwWristRotation = grip;
        throwPoseFrame = Time.frameCount;
        return true;
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
        if (ApplyRemoteThrow()) return;
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
