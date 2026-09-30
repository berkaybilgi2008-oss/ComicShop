using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Hareket")]
    public float walkSpeed = 4.5f;
    public float sprintSpeed = 7.5f;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public float jumpHeight = 1.4f;
    public float gravity = -20f;

    [Header("Mouse Bakis")]
    public float mouseSensitivity = 2.2f;
    public Transform cameraTransform;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    [Header("Kitap Yiginlari - Yumşak Gecis")]
    [Tooltip("Kitaplarin uzerine cikarken maksimum yukseltme mesafesi.")]
    [Min(0.05f)] public float bookWalkMaxRise = 0.42f;
    [Tooltip("Kitaplardan inerken kabul edilen maksimum yuzey farki.")]
    [Min(0.05f)] public float bookWalkMaxDrop = 0.5f;
    [Tooltip("Karakter ayaklarinin kitap yuzeyine ne kadar hizli oturacagi.")]
    [Min(1f)] public float bookWalkFollowSpeed = 10f;
    [Tooltip("Kitap yiginindan inerken ilk anda uygulanabilecek maksimum dusus hizi.")]
    [Min(0.1f)] public float bookWalkDropSpeed = 3.2f;

    private CharacterController controller;
    private Vector3 velocity;
    private float pitch = 0f;
    private PlayerKnockdown knockdown;
    private float lastBookSurfaceY;
    private bool hadBookSurface;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        knockdown = GetComponent<PlayerKnockdown>();
        if (knockdown == null) knockdown = gameObject.AddComponent<PlayerKnockdown>();

        // The physical book box remains exact for book/world physics, while
        // floor books use a dedicated soft walk surface for the player.
        BookWalkSurface.RegisterPlayer(controller);

        if (cameraTransform == null)
        {
            Camera camera = GetComponentInChildren<Camera>(true);
            if (camera != null) cameraTransform = camera.transform;
        }
    }

    void OnDestroy()
    {
        if (controller != null)
            BookWalkSurface.UnregisterPlayer(controller);
    }

    void Update()
    {
        if (ShopLoadingScreen.IsVisible) return;
        if (knockdown != null && knockdown.IsDown) { velocity = Vector3.zero; return; }
        if (Cursor.lockState != CursorLockMode.Locked || cameraTransform == null || !controller.enabled) return;
        HandleLook();
        HandleMove();
    }

    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * ShopSettings.Current.sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * ShopSettings.Current.sensitivity * (ShopSettings.Current.invertY ? -1 : 1);

        transform.Rotate(Vector3.up * mouseX);

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    void HandleMove()
    {
        bool bookGroundedBeforeMove = TryGetBookSurface(out float bookSurfaceYBeforeMove);
        bool isGrounded = controller.isGrounded || bookGroundedBeforeMove;

        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        float h = (Input.GetKey(ShopSettings.Key(ShopAction.Right)) ? 1 : 0) - (Input.GetKey(ShopSettings.Key(ShopAction.Left)) ? 1 : 0);
        float v = (Input.GetKey(ShopSettings.Key(ShopAction.Forward)) ? 1 : 0) - (Input.GetKey(ShopSettings.Key(ShopAction.Back)) ? 1 : 0);

        bool isSprinting = Input.GetKey(ShopSettings.Key(ShopAction.Sprint));
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        Vector3 move = Vector3.ClampMagnitude(transform.right * h + transform.forward * v, 1f);
        controller.Move(move * currentSpeed * Time.deltaTime);

        // Sample after horizontal movement. This makes the surface react to
        // the actual book under the player's new position, including stacks.
        bool bookGroundedAfterMove = TryGetBookSurface(out float bookSurfaceY);

        if (bookGroundedAfterMove)
        {
            lastBookSurfaceY = bookSurfaceY;
            hadBookSurface = true;

            float feetY = transform.position.y + controller.center.y - controller.height * 0.5f;
            float delta = bookSurfaceY - feetY;

            // Follow the ramp instead of letting CharacterController treat the
            // exact book box as a hard step. Exponential smoothing keeps the
            // transition soft at different frame rates.
            if (velocity.y <= 0f && Mathf.Abs(delta) <= bookWalkMaxRise + 0.05f)
            {
                float blend = 1f - Mathf.Exp(-bookWalkFollowSpeed * Time.deltaTime);
                controller.Move(Vector3.up * (delta * blend));
                velocity.y = 0f;
            }
        }
        else if (hadBookSurface && velocity.y <= 0f)
        {
            // We just left a book pile. Start the descent gently; gravity takes
            // over immediately afterwards, so larger drops still feel natural.
            velocity.y = Mathf.Max(velocity.y, -bookWalkDropSpeed);
            hadBookSurface = false;
        }

        if (Input.GetKeyDown(ShopSettings.Key(ShopAction.Jump)) && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            hadBookSurface = false;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // Avoid retaining a stale surface height when the player has clearly
        // moved away from the pile.
        if (hadBookSurface && Mathf.Abs(transform.position.y - lastBookSurfaceY) > bookWalkMaxDrop + 0.75f)
            hadBookSurface = false;
    }

    private bool TryGetBookSurface(out float surfaceY)
    {
        return BookWalkSurface.TryGetTopSurface(
            transform.position,
            controller,
            bookWalkMaxRise,
            bookWalkMaxDrop,
            out surfaceY);
    }
}
