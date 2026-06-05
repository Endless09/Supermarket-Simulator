using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Lightweight first-person controller for prototype testing.
/// Tab toggles between player view and cursor/UI mode.
/// </summary>
public class FirstPersonPlayerController : MonoBehaviour
{
    public static FirstPersonPlayerController Instance { get; private set; }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float sprintMultiplier = 1.6f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float eyeHeight = 0.95f;
    [SerializeField] private float bodyHeight = 1.8f;
    [SerializeField] private float fallResetHeight = -8f;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 0.08f;
    [SerializeField] private float minPitch = -75f;
    [SerializeField] private float maxPitch = 75f;

    [Header("Startup")]
    [SerializeField] private bool startInPlayerView = false;
    [SerializeField] private bool snapFromTopDownCamera = true;
    [SerializeField] private Vector3 firstPersonStartPosition = new Vector3(-4f, 0.95f, -6f);
    [SerializeField] private Vector3 firstPersonStartEuler = new Vector3(0f, 0f, 0f);

    private CharacterController characterController;
    private float verticalVelocity;
    private float cameraPitch;
    private bool isPlayerViewActive;
    private Vector3 resolvedSpawnPosition;

    public bool IsPlayerViewActive => isPlayerViewActive;
    public bool UseCenteredCursorForWorldInteractions => isPlayerViewActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureCharacterController();
    }

    private void Start()
    {
        resolvedSpawnPosition = ResolveSafeSpawnPosition();

        if (snapFromTopDownCamera && transform.position.y > 4f)
        {
            transform.position = resolvedSpawnPosition;
            transform.rotation = Quaternion.Euler(firstPersonStartEuler);
        }

        Vector3 euler = transform.eulerAngles;
        cameraPitch = NormalizeAngle(euler.x);
        SetPlayerViewActive(startInPlayerView);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        if (GetToggleViewPressed())
        {
            SetPlayerViewActive(!isPlayerViewActive);
        }

        if (GetUnlockPressed())
        {
            SetPlayerViewActive(false);
        }

        UpdateLook();
        UpdateMovement();
        RecoverIfOutOfBounds();
    }

    public string GetControlsSummary()
    {
        string modeLabel = isPlayerViewActive ? "Player View" : "Cursor/UI";
        return "Mode: " + modeLabel + "\nTab toggle, WASD move, Shift sprint, E interact";
    }

    private void EnsureCharacterController()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
        }

        float halfBodyHeight = bodyHeight * 0.5f;
        float centerYOffset = halfBodyHeight - eyeHeight;

        characterController.radius = 0.35f;
        characterController.height = bodyHeight;
        characterController.center = new Vector3(0f, centerYOffset, 0f);
        characterController.slopeLimit = 45f;
        characterController.stepOffset = 0.3f;
        characterController.minMoveDistance = 0f;
    }

    private void SetPlayerViewActive(bool isActive)
    {
        isPlayerViewActive = isActive;
        Cursor.lockState = isActive ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isActive;
    }

    private void UpdateLook()
    {
        if (!isPlayerViewActive)
        {
            return;
        }

        Vector2 lookDelta = GetLookDelta();
        float yawDelta = lookDelta.x * lookSensitivity;
        float pitchDelta = lookDelta.y * lookSensitivity;

        cameraPitch = Mathf.Clamp(cameraPitch - pitchDelta, minPitch, maxPitch);
        transform.rotation = Quaternion.Euler(cameraPitch, transform.eulerAngles.y + yawDelta, 0f);
    }

    private void UpdateMovement()
    {
        if (characterController == null)
        {
            return;
        }

        Vector2 moveInput = GetMoveInput();
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 moveDirection = (forward * moveInput.y) + (right * moveInput.x);

        float currentMoveSpeed = moveSpeed * (IsSprintHeld() ? sprintMultiplier : 1f);

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = (moveDirection * currentMoveSpeed) + (Vector3.up * verticalVelocity);
        characterController.Move(velocity * Time.deltaTime);
    }

    private void RecoverIfOutOfBounds()
    {
        if (transform.position.y >= fallResetHeight)
        {
            return;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        transform.position = ResolveSafeSpawnPosition();
        transform.rotation = Quaternion.Euler(firstPersonStartEuler);
        cameraPitch = NormalizeAngle(firstPersonStartEuler.x);
        verticalVelocity = 0f;

        if (characterController != null)
        {
            characterController.enabled = true;
        }
    }

    private Vector3 ResolveSafeSpawnPosition()
    {
        GameObject floorObject = GameObject.Find("Floor");
        Collider floorCollider = floorObject != null ? floorObject.GetComponent<Collider>() : null;
        if (floorCollider == null)
        {
            return firstPersonStartPosition;
        }

        Bounds floorBounds = floorCollider.bounds;
        Vector3 safePosition = floorBounds.center;
        safePosition.x -= floorBounds.extents.x * 0.25f;
        safePosition.z -= floorBounds.extents.z * 0.25f;
        safePosition.y = floorBounds.max.y + eyeHeight;
        resolvedSpawnPosition = safePosition;
        return resolvedSpawnPosition;
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }

    private Vector2 GetMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
        {
            return Vector2.zero;
        }

        Vector2 move = Vector2.zero;
        if (Keyboard.current.aKey.isPressed)
        {
            move.x -= 1f;
        }

        if (Keyboard.current.dKey.isPressed)
        {
            move.x += 1f;
        }

        if (Keyboard.current.sKey.isPressed)
        {
            move.y -= 1f;
        }

        if (Keyboard.current.wKey.isPressed)
        {
            move.y += 1f;
        }

        return Vector2.ClampMagnitude(move, 1f);
#else
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
    }

    private Vector2 GetLookDelta()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
    }

    private bool IsSprintHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
#else
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
    }

    private bool GetToggleViewPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Tab);
#endif
    }

    private bool GetUnlockPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
