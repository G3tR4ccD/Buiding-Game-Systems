using UnityEngine;
using UnityEngine.InputSystem;

// First-person style controller: WASD/stick movement relative to camera direction,
// mouse-look with a head that can turn independently of the body up to a limit,
// and jump timing that stays forgiving via coyote time and input buffering.
[DisallowMultipleComponent] // prevents accidentally adding a second copy of this script to the same GameObject
public class PlayerMovement : MonoBehaviour
{
    private CharacterController cc;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 velocity;      // Only the y component is really used, x/z movement is applied directly each frame
    private float xRotation = 0f;  // Head pitch (up/down), in degrees
    private float headYaw = 0f;    // Head yaw relative to the body (left/right), in degrees

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpHeight = 5f;
    private float gravity = -9.81f;

    [Header("Look Settings")]
    public Transform headTransform; // The actual animated head bone. Read from for position tracking only, never written to.
    public Transform cameraAnchor;  // Plain, non-animated object holding the actual Camera. This is what the script positions/rotates.
    public float headPositionSmoothSpeed = 10f; // How fast cameraAnchor's position catches up to headTransform's; higher = snappier, lower = smoother
    public float headLookInfluence = 0.5f; // How much the actual head bone visually turns toward the look direction. 0 = doesn't turn at all (previous behavior). 1 = fully matches look direction (same hard override that caused the original wobble). This blends rather than replaces, so animation still comes through somewhat; may need its own axis tuning separate from cameraAnchor, since the rig bone doesn't necessarily share cameraAnchor's orientation convention.
    public float headPitchMin = -90f; // How far this bone tilts down, independent of the camera's own look range
    public float headPitchMax = 90f;  // How far this bone tilts up, independent of the camera's own look range
    public float lookSensitivity = 10f;
    public float maxHeadYaw = 80f;      // How far the head can turn from the body before the body starts turning too
    public float bodyTurnSpeed = 180f;  // Degrees per second the body turns to catch up with the camera while walking

    [Header("Jump Feel")]
    public float coyoteTime = 0.15f;     // Grace period after walking off a ledge where a jump still counts
    public float jumpBufferTime = 0.15f; // Window before landing where a jump press still counts
    private float coyoteTimer;
    private float jumpBufferTimer;

    private bool inputEnabled = true;              // False while a menu (e.g. inventory) has taken over input
    private float lookSuppressTimer = 0f;          // While > 0, incoming look input is discarded instead of applied
    public float lookSuppressDuration = 0.15f;     // How long to ignore look input after toggling, covers a delayed cursor-warp spike

    [Header("Inventory Link")]
    public Inventory inventory; // Every inventory-related input (toggle, hotbar select, scroll, debug items) routes through here

    [Header("Combat Link")]
    public AttackCombo attackCombo; // Fire input routes through here, same pattern as the inventory link above

    [Header("Animation")]
    public Animator animator; // Drives Speed (float) and Jump (trigger) on the character's Animator Controller
    public float jumpAnimationDelay = 0f; // Seconds between the jump input being validated and the jump animation starting to play
    public float jumpForceDelay = 0f; // Seconds between the jump animation starting and the actual upward force being applied, matches the animation's windup/anticipation portion
    public float jumpAnimationClipLength = 1f; // Your jump clip's actual length in seconds (check the clip's own Inspector). Used to auto-scale its playback speed to match the real physics air time below.
    private float jumpAnimTimer = -1f;  // Counts down to firing the trigger; -1 means nothing's pending
    private float jumpForceTimer = -1f; // Counts down to applying the actual jump force, starts once the trigger above fires

    [Header("Debug")]
    public int debugSpawnAmount = 1; // how many of each item the "spawn all" debug key gives
    private ItemSO[] allDebugItems;  // every ItemSO found under Resources/Items, loaded once in Start

    [Header("Debug Health")]
    public Health playerHealth;       // Drag the player's Health component in to enable the debug damage/heal keys
    public float debugDamageAmount = 10f;
    public float debugHealAmount = 10f;

    // Call this from Inventory (or any other menu) when it opens or closes, so the player
    // doesn't spin the camera or walk around while a UI panel is up.
    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;

        // Toggling Cursor.lockState (done by whatever called this) warps the OS cursor, which can
        // make a Mouse.delta reading spike to a huge value, sometimes on the very next frame,
        // sometimes a frame or two later depending on platform. Ignoring look input for a short
        // window rather than just one frame keeps the camera from snapping either way.
        moveInput = Vector2.zero;
        lookInput = Vector2.zero;
        lookSuppressTimer = lookSuppressDuration;
    }

    private Quaternion headBoneRestRotation = Quaternion.identity; // The head bone's natural rest orientation, captured before animation ever runs, so the look rotation offsets from this instead of from absolute zero

    private void Awake()
    {
        cc = GetComponent<CharacterController>();

        if (headTransform != null)
        {
            headBoneRestRotation = headTransform.localRotation;
        }
    }

    private void Start()
    {
        // Lock and hide the cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Snap to the head bone's actual position immediately, rather than wherever it happened
        // to be placed in the Editor. Without this, the smoothing in LateUpdate() would spend
        // the first moment or two visibly gliding to close whatever gap it started with.
        if (cameraAnchor != null && headTransform != null)
        {
            cameraAnchor.position = headTransform.position;
        }

        // Loads every ItemSO under Assets/Resources/Items/. New items just need to sit in
        // that folder to show up here, nothing else to wire up. Cached once since
        // Resources.LoadAll isn't cheap enough to call every time the debug key is pressed.
        allDebugItems = Resources.LoadAll<ItemSO>("Items");
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
        // Just record the press here. The jump itself is applied in Update(), right after
        // a fresh ground check, so it stays synced no matter when this callback happens
        // to fire relative to the frame.
        if (context.started)
        {
            jumpBufferTimer = jumpBufferTime;
        }
    }

    // Hook this up to your ToggleInventory Action Unity Event
    public void OnToggleInventory(InputAction.CallbackContext context)
    {
        if (context.started && inventory != null)
        {
            inventory.ToggleInventory();
        }
    }

    // Hook this up to your SelectHotbarSlot Action Unity Event (bound to keys 1-5).
    // context.control.name is the actual key that was pressed ("1", "2", etc.), since
    // one action covers all five number keys. The index gets handed to Inventory as
    // a plain int, it decides what actually happens with it.
    public void OnSelectHotbarSlot(InputAction.CallbackContext context)
    {
        if (!context.started || inventory == null)
        {
            return;
        }

        if (int.TryParse(context.control.name, out int slotNumber))
        {
            inventory.SelectOrSwapHotbarSlot(slotNumber - 1);
        }
    }

    // Hook this up to your Fire Action Unity Event (bound to left mouse click / right trigger)
    public void OnFire(InputAction.CallbackContext context)
    {
        if (context.started && inputEnabled && attackCombo != null)
        {
            attackCombo.TryAttack();
        }
    }

    // Hook this up to your ScrollHotbar Action Unity Event (bound to the mouse scroll wheel)
    public void OnScrollHotbar(InputAction.CallbackContext context)
    {
        // Value actions like this one invoke on multiple phases (started, performed) for a
        // single scroll tick. Movement/Look can ignore that since they just overwrite a value,
        // but this steps the selection by +1/-1, so without this guard one tick moves two slots.
        if (!context.performed || inventory == null)
        {
            return;
        }

        float scrollY = context.ReadValue<Vector2>().y;

        if (scrollY > 0f)
        {
            inventory.ScrollHotbar(-1); // scroll up = previous slot
        }
        else if (scrollY < 0f)
        {
            inventory.ScrollHotbar(1); // scroll down = next slot
        }
    }

    private void Update()
    {
        // Debug/test only, deliberately runs even while inputEnabled is false so it still
        // works with the inventory open, matching how this behaved before it lived here.
        HandleDebugItemKeys();
        HandleDebugHealthKeys();

        if (!inputEnabled)
        {
            return;
        }

        // Handle rotation first
        HandleLook();

        bool isGrounded = cc.isGrounded; // read once, reused below instead of calling the property twice

        Vector2 clampedInput = Vector2.ClampMagnitude(moveInput, 1f); // Normalize input to prevent faster diagonal movement

        if (animator != null)
        {
            animator.SetFloat("Speed", clampedInput.magnitude);

            // Same jump velocity formula used below when the jump is actually applied. Total air
            // time for a symmetric up-and-down arc is twice the time to reach the peak, so the
            // clip's real length divided by that gives exactly the multiplier needed to make the
            // animation's duration match how long the character is genuinely airborne.
            float jumpVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            float jumpAirTime = 2f * jumpVelocity / -gravity;

            if (jumpAirTime > 0f)
            {
                animator.SetFloat("JumpAnimSpeed", jumpAnimationClipLength / jumpAirTime);
            }
        }

        // While actually walking, gradually turn the body toward the camera direction
        // so it doesn't stay locked in place until the hard head-turn limit is hit
        if (clampedInput.sqrMagnitude > 0.0001f && Mathf.Abs(headYaw) > 0.01f)
        {
            float maxDegreesThisFrame = bodyTurnSpeed * Time.deltaTime;
            float turnAmount = Mathf.Clamp(headYaw, -maxDegreesThisFrame, maxDegreesThisFrame);

            transform.Rotate(Vector3.up * turnAmount);
            headYaw -= turnAmount;
        }

        // Coyote time: keep a short grace window open after leaving the ground
        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }

        // Jump buffer: keep a short grace window open after the button was pressed
        jumpBufferTimer -= Time.deltaTime;

        // Handle gravity physics
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // small downward value keeps the controller flush with the ground instead of accumulating fall speed
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        // A jump fires as long as both windows are still open. This covers a press
        // made just before landing, and a press made just after walking off an edge.
        // The actual upward force no longer happens here, it's queued below in two
        // stages so the animation's windup can play out first if you want one.
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;

            jumpAnimTimer = jumpAnimationDelay; // stage 1: wait, then start the animation
        }

        // Stage 1: once the delay above elapses, fire the animation trigger and queue stage 2
        if (jumpAnimTimer >= 0f)
        {
            jumpAnimTimer -= Time.deltaTime;

            if (jumpAnimTimer <= 0f)
            {
                if (animator != null)
                {
                    animator.SetTrigger("Jump");
                }

                jumpAnimTimer = -1f;
                jumpForceTimer = jumpForceDelay; // stage 2: wait out the windup, then actually leave the ground
            }
        }

        // Stage 2: once the windup elapses, apply the actual jump force. The character stays
        // grounded and still the whole time this is counting down, gravity handling above
        // keeps it flush with the floor, so nothing looks wrong while the windup plays.
        if (jumpForceTimer >= 0f)
        {
            jumpForceTimer -= Time.deltaTime;

            if (jumpForceTimer <= 0f)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpForceTimer = -1f;
            }
        }

        // Calculate and apply movement relative to where the camera is looking, not where the body is facing.
        // Built directly from yaw angles rather than headTransform.forward/right: this model's pitch
        // rotates around a non-standard local axis, so once pitch and yaw combine into one rotation,
        // .forward/.right don't reliably point where they normally would. Yaw itself is already known
        // correct (turning works), so the body's world yaw plus the head's relative offset gives a
        // clean horizontal direction with no pitch involved at all.
        Quaternion yawOnlyRotation = Quaternion.Euler(0f, transform.eulerAngles.y + headYaw, 0f);

        Vector3 headForward = yawOnlyRotation * Vector3.forward;
        Vector3 headRight = yawOnlyRotation * Vector3.right;

        Vector3 moveDirection = (headForward * clampedInput.y + headRight * clampedInput.x) * moveSpeed;
        moveDirection.y = velocity.y; // vertical speed (gravity/jump) rides along with the horizontal movement

        cc.Move(moveDirection * Time.deltaTime);
    }

    private void HandleLook()
    {
        if (lookSuppressTimer > 0f)
        {
            lookSuppressTimer -= Time.deltaTime;
            lookInput = Vector2.zero;
            return; // discard this frame's look input entirely, xRotation/headYaw don't move at all
        }

        // Mouse Delta doesn't need Time.deltaTime multiplication in the New Input System
        float mouseX = lookInput.x * lookSensitivity * 0.1f;
        float mouseY = lookInput.y * lookSensitivity * 0.1f;

        // Vertical look stays on the head only (clamp to prevent flipping past straight up/down)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Horizontal look accumulates on the head first, not the body
        headYaw += mouseX;

        // Once the head passes the limit, the extra rotation gets passed to the body instead,
        // so the body swings around to catch up rather than snapping instantly with the head
        if (headYaw > maxHeadYaw)
        {
            float excess = headYaw - maxHeadYaw;
            transform.Rotate(Vector3.up * excess);
            headYaw = maxHeadYaw;
        }
        else if (headYaw < -maxHeadYaw)
        {
            float excess = headYaw + maxHeadYaw;
            transform.Rotate(Vector3.up * excess);
            headYaw = -maxHeadYaw;
        }
    }

    // Runs after the Animator has already applied this frame's bone poses (Unity's order is
    // Update -> Animator -> LateUpdate). cameraAnchor isn't part of the animated skeleton at
    // all, so nothing here is "overriding" animation, it's just never touched by it in the
    // first place. Rotation is purely player-controlled. Position smoothly tracks wherever
    // headTransform (the real animated bone) currently is, so genuine pose changes, like
    // standing tall at idle versus laying low while trotting, still come through correctly,
    // while fast per-frame jitter within a single animation gets smoothed out instead of
    // passed straight to the camera.
    private void LateUpdate()
    {
        if (cameraAnchor == null)
        {
            return;
        }

        cameraAnchor.localRotation = Quaternion.Euler(xRotation, headYaw, 0f);

        if (headTransform != null)
        {
            cameraAnchor.position = Vector3.Lerp(cameraAnchor.position, headTransform.position, headPositionSmoothSpeed * Time.deltaTime);

            // Blends a look rotation onto whatever the animation already set this frame, rather
            // than replacing it outright. Uses the same X/Y mapping as cameraAnchor as a first
            // attempt; if this looks wrong (inverted, or at an odd angle), it means the rig bone
            // needs its own axis tuning independent of cameraAnchor's.
            if (headLookInfluence > 0f)
            {
                float headPitch = Mathf.Clamp(-xRotation, headPitchMin, headPitchMax);
                Quaternion lookOffset = Quaternion.Euler(0f, -headYaw, headPitch); // yaw sign flipped for this bone specifically, pitch uses its own separate range
                Quaternion lookRotation = lookOffset * headBoneRestRotation; // trying the other multiplication order, composition order changes the result
                headTransform.localRotation = Quaternion.Slerp(headTransform.localRotation, lookRotation, headLookInfluence);
            }
        }
    }

    // Debug/test only: press P to give the player one (or debugSpawnAmount) of every
    // ItemSO found under Assets/Resources/Items. Add new items there and they show up
    // here automatically, no code changes needed. Swap this out once real item sources exist.
    private void HandleDebugItemKeys()
    {
        if (inventory == null || allDebugItems == null)
        {
            return;
        }

        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            foreach (ItemSO item in allDebugItems)
            {
                inventory.AddItem(item, debugSpawnAmount);
            }
        }
    }

    // Debug/test only: press O to take debugDamageAmount damage, or U to heal by
    // debugHealAmount. Swap this out once real damage sources and healing items exist.
    private void HandleDebugHealthKeys()
    {
        if (playerHealth == null || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.oKey.wasPressedThisFrame)
        {
            playerHealth.TakeDamage(debugDamageAmount);
        }

        if (Keyboard.current.uKey.wasPressedThisFrame)
        {
            playerHealth.Heal(debugHealAmount);
        }
    }

    // --- Shared control queries ---
    // Static so other scripts (e.g. Slot, for its shift/ctrl click behavior) can check
    // modifier keys without needing their own reference to a PlayerMovement instance
    // or their own UnityEngine.InputSystem code.

    public static bool IsShiftHeld()
    {
        return Keyboard.current != null &&
            (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
    }

    public static bool IsCtrlHeld()
    {
        return Keyboard.current != null &&
            (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
    }
}