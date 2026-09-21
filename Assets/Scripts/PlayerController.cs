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
    public Transform cameraTransform; // Main Camera'yi buraya surukle
    public float minPitch = -80f;
    public float maxPitch = 80f;

    private CharacterController controller;
    private Vector3 velocity;
    private float pitch = 0f;
    private PlayerKnockdown knockdown;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        knockdown = GetComponent<PlayerKnockdown>();
        if (knockdown == null) knockdown = gameObject.AddComponent<PlayerKnockdown>();
        if (cameraTransform == null)
        {
            Camera camera = GetComponentInChildren<Camera>(true);
            if (camera != null) cameraTransform = camera.transform;
        }
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
        bool isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        float h = (Input.GetKey(ShopSettings.Key(ShopAction.Right)) ? 1 : 0) - (Input.GetKey(ShopSettings.Key(ShopAction.Left)) ? 1 : 0);
        float v = (Input.GetKey(ShopSettings.Key(ShopAction.Forward)) ? 1 : 0) - (Input.GetKey(ShopSettings.Key(ShopAction.Back)) ? 1 : 0);

        bool isSprinting = Input.GetKey(ShopSettings.Key(ShopAction.Sprint));
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        Vector3 move = Vector3.ClampMagnitude(transform.right * h + transform.forward * v, 1f);
        controller.Move(move * currentSpeed * Time.deltaTime);

        if (Input.GetKeyDown(ShopSettings.Key(ShopAction.Jump)) && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
