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

    [Header("Mouse Bakış")]
    public float mouseSensitivity = 2.2f;
    public Transform cameraTransform; // Main Camera'yı buraya sürükle
    public float minPitch = -80f;
    public float maxPitch = 80f;

    private CharacterController controller;
    private Vector3 velocity;
    private float pitch = 0f; // kameranın yukarı/aşağı açısı

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleLook();
        HandleMove();
    }

    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Govdeyi (Player objesini) yatayda dondur
        transform.Rotate(Vector3.up * mouseX);

        // Kamerayi dikeyde dondur, ama klampla (takla atmasin)
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    void HandleMove()
    {
        bool isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f; // yere yapisik dursun, surekli tam sifir olursa titreme yapar

        float h = Input.GetAxis("Horizontal"); // A/D
        float v = Input.GetAxis("Vertical");   // W/S

        // Sprint tusuna basiliyken hizli, degilse normal hizda yuru
        bool isSprinting = Input.GetKey(sprintKey);
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        Vector3 move = transform.right * h + transform.forward * v;
        controller.Move(move * currentSpeed * Time.deltaTime);

        // Ziplama
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            // fizik formulu: v = sqrt(h * -2 * g)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}