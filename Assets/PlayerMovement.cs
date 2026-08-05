using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private CharacterController cc;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 velocity;
    private float xRotation = 0f;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpHeight = 5f;
    private float gravity = -9.81f;

    [Header("Look Settings")]
    public Transform headTransform;
    public float lookSensitivity = 10f;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    private void Start()
    {
        // Lock and hide the cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Hook this up to your Look Action Unity Event
    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    // Hook this up to your Movement Action Unity Event
    public void OnMovement(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    // Hook this up to your Jump Action Unity Event
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started && cc.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    private void Update()
    {
        // Handle rotation first
        HandleLook();

        // Handle gravity physics
        if (cc.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        // Calculate and apply movement relative to player rotation
        Vector2 clampedInput = Vector2.ClampMagnitude(moveInput, 1f); // Normalize input to prevent faster diagonal movement
        Vector3 moveDirection = (transform.forward * clampedInput.y + transform.right * clampedInput.x) * moveSpeed; // Move relative to player rotation, scaled by moveSpeed

        moveDirection.y = velocity.y; // Set the vertical component of the moveDirection vector to the current vertical velocity

        cc.Move(moveDirection * Time.deltaTime); // Move the player using the CharacterController's Move method, applying the moveDirection vector scaled by Time.deltaTime to ensure smooth movement
    }

    private void HandleLook()
    {
        // Mouse Delta doesn't need Time.deltaTime multiplication in the New Input System
        float mouseX = lookInput.x * lookSensitivity * 0.1f;
        float mouseY = lookInput.y * lookSensitivity * 0.1f;

        // Rotate Head up/down (Clamp to prevent flipping)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        headTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Rotate Capsule body left/right
        transform.Rotate(Vector3.up * mouseX);
    }
}