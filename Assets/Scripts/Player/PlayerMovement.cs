using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;

    [Header("Camera")]
    public Camera playerCamera;
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 90f;

    private CharacterController controller;
    private InputSystem_Actions inputActions;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 velocity;
    private bool isGrounded;
    private float xRotation = 0f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputActions = new InputSystem_Actions();
    }

    void OnEnable()
    {
        inputActions.Player.Enable();

        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += _ => moveInput = Vector2.zero;

        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += _ => lookInput = Vector2.zero;

        inputActions.Player.Jump.performed += _ => Jump();
    }

    void OnDisable()
    {
        inputActions.Player.Disable();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

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
        }
    }

    void Update()
    {
        HandleCameraRotation();
        HandleMovement();
        ApplyGravity();
    }

    void HandleMovement()
    {
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * moveSpeed * Time.deltaTime);
        controller.Move(velocity * Time.deltaTime);
    }

    void ApplyGravity()
    {
        isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
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
        if (!isGrounded) return;

        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }
}
