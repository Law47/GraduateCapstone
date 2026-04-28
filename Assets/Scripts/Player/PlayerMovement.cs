using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float groundAcceleration = 40f;
    [SerializeField] private float airAcceleration = 15f;
    [SerializeField] private float slideAcceleration = 12f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float groundedSnapVelocity = 2f;
    [SerializeField] private float groundDrag = 5f;
    [SerializeField] private float airDrag = 0.1f;

    [Header("Crouch & Slide")]
    [SerializeField] private float slideToCrouchSpeedThreshold = -1f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchScale = 0.5f;
    [SerializeField] private float slideFriction = 0.1f;
    [SerializeField] private float crouchFriction = 5f;
    [SerializeField] private float slideSteeringMultiplier = 0.35f;
    [SerializeField] private float slideDeceleration = 8f;
    [SerializeField] private float crouchCameraOffset = 0.5f;
    [SerializeField] private Transform crouchVisualTarget;

    [Header("Dash")]
    [SerializeField] private float dashDistance = 7f;
    [SerializeField] private float dashDuration = 0.25f;
    [SerializeField] private float dashCooldown = 0.5f;

    [Header("Camera")]
    public Camera playerCamera;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 90f;

    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private InputSystem_Actions inputActions;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool isGrounded;
    private bool crouchHeld;
    private float xRotation = 0f;
    private float dashCooldownTimer = 0f;
    private Vector3 cameraDefaultLocalPosition;
    private Vector3 crouchVisualDefaultLocalScale;
    private Vector3 groundNormal = Vector3.up;
    private Vector3 capsuleDefaultCenter;
    private float capsuleDefaultHeight;
    private float capsuleBottomOffset;
    private bool isDashing;
    private float dashTimer;
    private Vector3 dashDirection;

    private enum MovementState { Normal, Crouch, Slide }
    private MovementState currentState = MovementState.Normal;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        inputActions = new InputSystem_Actions();

        if (capsuleCollider == null)
        {
            Debug.LogWarning("PlayerMovement expects a CapsuleCollider for crouch/slide sizing.");
        }
        else
        {
            capsuleDefaultCenter = capsuleCollider.center;
            capsuleDefaultHeight = capsuleCollider.height;
            capsuleBottomOffset = capsuleDefaultCenter.y - (capsuleDefaultHeight * 0.5f);
        }

        if (crouchVisualTarget != null)
        {
            crouchVisualDefaultLocalScale = crouchVisualTarget.localScale;
        }

        if (playerCamera != null)
        {
            cameraDefaultLocalPosition = playerCamera.transform.localPosition;
        }
    }

    void OnEnable()
    {
        // Re-subscribe input when the component is re-enabled (e.g. after unpausing).
        // IsSpawned guards against OnEnable firing before OnNetworkSpawn.
        if (IsSpawned && IsOwner)
            EnableOwnerInput();
    }

    void OnDisable()
    {
        crouchHeld = false;
        isDashing = false;
        dashTimer = 0f;
        DisableOwnerInput();
    }

    public override void OnNetworkDespawn()
    {
        DisableOwnerInput();
        base.OnNetworkDespawn();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        rb.isKinematic = !IsOwner;
        crouchHeld = false;
        currentState = MovementState.Normal;

        AudioListener audioListener = GetComponent<AudioListener>();
        if (audioListener != null)
        {
            audioListener.enabled = IsOwner;
        }

        foreach (var listener in GetComponentsInChildren<AudioListener>())
        {
            listener.enabled = IsOwner;
        }

        if (playerCamera != null)
        {
            playerCamera.enabled = IsOwner;
        }

        foreach (var camera in GetComponentsInChildren<Camera>())
        {
            camera.enabled = IsOwner;
        }

        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            EnableOwnerInput();
        }

        ApplyStance(currentState);
    }

    void Update()
    {
        if (!IsOwner || rb.isKinematic) return;

        crouchHeld = inputActions.Player.Crouch.IsPressed();
        HandleCameraRotation();
        UpdateMovementState();
        dashCooldownTimer -= Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (!IsOwner || rb.isKinematic) return;

        CheckGrounded();
        HandleMovement();
        ApplyGravity();
        ApplyDrag();
    }

    void CheckGrounded()
    {
        Collider col = capsuleCollider != null ? capsuleCollider : GetComponent<Collider>();
        if (col == null)
        {
            isGrounded = false;
            groundNormal = Vector3.up;
            return;
        }

        Vector3 rayOrigin = col.bounds.center;
        float rayDistance = col.bounds.extents.y + 0.1f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance))
        {
            isGrounded = true;
            groundNormal = hit.normal;
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
        }
    }

    void HandleMovement()
    {
        if (isDashing)
        {
            ApplyDashHorizontalVelocity();

            dashTimer -= Time.fixedDeltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
                dashTimer = 0f;
            }

            return;
        }

        Vector3 moveDirection = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;

        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 targetVelocity = moveDirection * moveSpeed;
        float acceleration = isGrounded ? groundAcceleration : airAcceleration;

        if (currentState == MovementState.Slide)
        {
            float currentSpeed = horizontalVelocity.magnitude;
            float nextSpeed = Mathf.Max(0f, currentSpeed - (slideDeceleration * Time.fixedDeltaTime));

            Vector3 slideDirection = currentSpeed > 0.01f ? horizontalVelocity.normalized : transform.forward;
            if (moveDirection.sqrMagnitude > 0.01f)
            {
                slideDirection = Vector3.Slerp(slideDirection, moveDirection, slideSteeringMultiplier);
                slideDirection.Normalize();
            }

            targetVelocity = slideDirection * nextSpeed;
            acceleration = slideAcceleration;
        }

        Vector3 nextHorizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(nextHorizontalVelocity.x, rb.linearVelocity.y, nextHorizontalVelocity.z);
    }

    void ApplyGravity()
    {
        if (isGrounded && rb.linearVelocity.y < 0)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, -groundedSnapVelocity, rb.linearVelocity.z);
        }
        else if (!isGrounded)
        {
            rb.linearVelocity += Vector3.up * gravity * Time.fixedDeltaTime;
        }
    }

    void ApplyDrag()
    {
        if (isDashing)
        {
            rb.linearDamping = 0f;
            return;
        }

        float currentDrag = groundDrag;
        
        if (!isGrounded)
        {
            currentDrag = airDrag;
        }
        else if (currentState == MovementState.Slide)
        {
            currentDrag = slideFriction;
        }
        else if (currentState == MovementState.Crouch)
        {
            currentDrag = crouchFriction;
        }

        rb.linearDamping = currentDrag;
    }

    void UpdateMovementState()
    {
        MovementState newState = MovementState.Normal;
        float horizontalSpeed = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude;
        float crouchThreshold = slideToCrouchSpeedThreshold > 0f ? slideToCrouchSpeedThreshold : moveSpeed;

        if (crouchHeld)
        {
            newState = horizontalSpeed >= crouchThreshold ? MovementState.Slide : MovementState.Crouch;
        }

        if (currentState != newState)
        {
            SetStateServerRpc(newState);
        }
    }

    void HandleCameraRotation()
    {
        if (playerCamera == null) return;

        transform.Rotate(Vector3.up * lookInput.x * mouseSensitivity * Time.deltaTime);

        xRotation -= lookInput.y * mouseSensitivity * Time.deltaTime;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    void Jump()
    {
        if (!IsOwner || rb.isKinematic) return;
        if (!isGrounded) return;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        rb.linearVelocity += Vector3.up * Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    void Dash()
    {
        if (!IsOwner || rb.isKinematic) return;
        if (isDashing) return;
        if (dashCooldownTimer > 0) return;
        if (moveInput == Vector2.zero) return;

        dashDirection = (transform.right * moveInput.x + transform.forward * moveInput.y).normalized;
        dashTimer = Mathf.Max(0.01f, dashDuration);
        isDashing = true;
        ApplyDashHorizontalVelocity();
        dashCooldownTimer = dashCooldown;
    }

    void ApplyDashHorizontalVelocity()
    {
        float dashSpeed = dashDistance / Mathf.Max(0.01f, dashDuration);
        rb.linearVelocity = new Vector3(dashDirection.x * dashSpeed, rb.linearVelocity.y, dashDirection.z * dashSpeed);
    }

    void EnableOwnerInput()
    {
        // Unsubscribe first to prevent double-subscription if called more than once.
        inputActions.Player.Move.performed -= OnMovePerformed;
        inputActions.Player.Move.canceled -= OnMoveCanceled;
        inputActions.Player.Look.performed -= OnLookPerformed;
        inputActions.Player.Look.canceled -= OnLookCanceled;
        inputActions.Player.Jump.performed -= OnJumpPerformed;
        inputActions.Player.Dash.performed -= OnDashPerformed;

        inputActions.Player.Move.performed += OnMovePerformed;
        inputActions.Player.Move.canceled += OnMoveCanceled;
        inputActions.Player.Look.performed += OnLookPerformed;
        inputActions.Player.Look.canceled += OnLookCanceled;
        inputActions.Player.Jump.performed += OnJumpPerformed;
        inputActions.Player.Dash.performed += OnDashPerformed;
        inputActions.Player.Enable();
    }

    void DisableOwnerInput()
    {
        inputActions.Player.Move.performed -= OnMovePerformed;
        inputActions.Player.Move.canceled -= OnMoveCanceled;
        inputActions.Player.Look.performed -= OnLookPerformed;
        inputActions.Player.Look.canceled -= OnLookCanceled;
        inputActions.Player.Jump.performed -= OnJumpPerformed;
        inputActions.Player.Dash.performed -= OnDashPerformed;
        inputActions.Player.Disable();
    }

    void OnMovePerformed(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();
    void OnMoveCanceled(InputAction.CallbackContext _) => moveInput = Vector2.zero;
    void OnLookPerformed(InputAction.CallbackContext ctx) => lookInput = ctx.ReadValue<Vector2>();
    void OnLookCanceled(InputAction.CallbackContext _) => lookInput = Vector2.zero;
    void OnJumpPerformed(InputAction.CallbackContext _) => Jump();
    void OnDashPerformed(InputAction.CallbackContext _) => Dash();

    void ApplyStance(MovementState stance)
    {
        if (capsuleCollider != null)
        {
            float targetHeight = stance == MovementState.Normal ? capsuleDefaultHeight : crouchHeight;
            capsuleCollider.height = targetHeight;
            float targetCenterY = capsuleBottomOffset + (targetHeight * 0.5f);
            capsuleCollider.center = new Vector3(capsuleDefaultCenter.x, targetCenterY, capsuleDefaultCenter.z);
        }

        if (crouchVisualTarget != null)
        {
            float targetScaleY = stance == MovementState.Normal ? 1f : crouchScale;
            crouchVisualTarget.localScale = new Vector3(
                crouchVisualDefaultLocalScale.x,
                crouchVisualDefaultLocalScale.y * targetScaleY,
                crouchVisualDefaultLocalScale.z);
        }

        if (playerCamera != null)
        {
            float targetOffset = stance == MovementState.Normal ? 0f : -Mathf.Abs(crouchCameraOffset);
            playerCamera.transform.localPosition = cameraDefaultLocalPosition + new Vector3(0f, targetOffset, 0f);
        }
    }

    [Rpc(SendTo.Server)]
    void SetStateServerRpc(MovementState newState)
    {
        if (currentState == newState)
        {
            return;
        }

        currentState = newState;
        SetStateClientRpc(newState);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void SetStateClientRpc(MovementState newState)
    {
        currentState = newState;
        ApplyStance(newState);
    }

    /// <summary>
    /// Called on the owner client when the player dies or respawns.
    /// On death: detaches the camera (it lingers at the kill position) and freezes physics/input.
    /// On respawn: teleports the player, re-attaches the camera, and restores control.
    /// No-op on non-owner clients.
    /// </summary>
    public void SetDeadStateOnOwner(bool isDead, Vector3 respawnPosition = default)
    {
        if (!IsOwner)
            return;

        if (isDead)
        {
            DisableOwnerInput();
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;

            // Detach the camera so it stays at the world-space death position.
            if (playerCamera != null)
                playerCamera.transform.SetParent(null, worldPositionStays: true);
        }
        else
        {
            // Move the Rigidbody to the spawn point before re-attaching the camera.
            rb.position = respawnPosition;
            transform.position = respawnPosition;

            // Re-attach the camera. worldPositionStays:true keeps it at its current
            // world position momentarily; we then immediately snap the local position
            // to the default offset so no interpolation frame is visible.
            if (playerCamera != null)
            {
                playerCamera.transform.SetParent(transform, worldPositionStays: true);
                playerCamera.transform.localRotation = Quaternion.identity;
                xRotation = 0f;
            }

            // Reset stance \u2014 this sets the correct capsule size AND camera local position.
            currentState = MovementState.Normal;
            ApplyStance(MovementState.Normal);

            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            EnableOwnerInput();
        }
    }
}
