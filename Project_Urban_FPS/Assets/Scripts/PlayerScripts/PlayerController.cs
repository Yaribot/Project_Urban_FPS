using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private PlayerInputActions _inputActions;
    private CharacterController _characterController;
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private Vector3 _velocity;
    private bool _jumpPressed;
    private bool _isSprinting;
    private bool _isCrouching;
    private float _xRotation = 0f;
    private float _bobTimer = 0f;
    private Vector3 _defaultCamPos;
    private Vector3 _bobTargetDefaultPos;
    private Vector3 _bobOffset;
    private float _currentCamY;

    [Header ("Move Settings")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 9f;
    public float crouchSpeed = 2.5f;
    public float jumpHeight = 1.5f;
    public float gravity = -20f;
    public float mouseSensitivity = 0.15f; // lower = slower feel
    public Transform cameraHolder;
    public Transform cameraBobTarget;
    public float standHeight = 1.8f;
    public float crouchHeight = 0.9f;
    public float crouchSmooth = 8f;    // lerp speed
    public float bobFrequency = 8f;
    public float bobAmplitude = 0.04f;

    [Header ("Camera Settings")]
    public CinemachineCamera cinemachineCam;
    public float normalFOV = 60f;
    public float sprintFOV = 75f;
    public float crouchFOV = 50f;
    public float fovSpeed = 6f;


    private void Awake()
    {
        _inputActions = new PlayerInputActions();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _bobTargetDefaultPos = cameraBobTarget.localPosition;
        _defaultCamPos = cameraHolder.localPosition;
        _currentCamY = cameraHolder.localPosition.y;
    }

    // Update is called once per frame
    void Update()
    {
        Movement();
        LookRotation();
        UpdateFOV();
        
    }
    private void FixedUpdate()
    {
        
    }

    private void LateUpdate()
    {
        HandleHeadBob(); // needs to run before SmoothCameraHeight()...
        SmoothCameraHeight();
    }
    private void Movement()
    {
        // Ground stick
        if (_characterController.isGrounded && _velocity.y < 0) _velocity.y = -2f;

        // Jump (consumed once per press)
        if (_jumpPressed && _characterController.isGrounded)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpPressed = false;
        }

        // Horizontal move
        float speed = _isCrouching ? crouchSpeed : _isSprinting ? sprintSpeed : walkSpeed;
        Vector3 move = (transform.right * _moveInput.x + transform.forward * _moveInput.y).normalized;
        if (move.magnitude < 0.1f) move = Vector3.zero; // dead-zone

        _characterController.Move((move * speed + _velocity) * Time.deltaTime);

        // Apply gravity
        _velocity.y += gravity * Time.deltaTime;
    }

    private void LookRotation()
    {
        // Horizontal — rotate whole player body
        transform.Rotate(Vector3.up * _lookInput.x * mouseSensitivity);

        // Vertical — tilt CameraHolder only, clamped
        _xRotation -= _lookInput.y * mouseSensitivity;
        _xRotation = Mathf.Clamp(_xRotation, -80f, 80f);
        cameraHolder.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
    }

    private void ToggleCrouch()
    {
        // Trying to stand? Check ceiling first
        if (_isCrouching)
        {
            bool blocked = Physics.SphereCast(
                transform.position, 0.3f, Vector3.up,
                out _, standHeight - crouchHeight);
            if (blocked) return;
        }

        _isCrouching = !_isCrouching;
        _characterController.height = _isCrouching ? crouchHeight : standHeight;
        _characterController.center = Vector3.up * (_characterController.height * 0.5f);
    }

    private void SmoothCameraHeight()
    {
        float targetY = _isCrouching ? crouchHeight * 0.85f : standHeight * 0.9f;

        _currentCamY = Mathf.Lerp(_currentCamY, targetY, Time.deltaTime * crouchSmooth);
        if (Mathf.Abs(_currentCamY - targetY) < 0.001f) _currentCamY = targetY;

        cameraHolder.localPosition = new Vector3(cameraHolder.localPosition.x, _currentCamY, cameraHolder.localPosition.z);
    }

    private void UpdateFOV()
    {
        float target = _isCrouching ? crouchFOV
                 : _isSprinting ? sprintFOV
                 : normalFOV;

        float current = cinemachineCam.Lens.FieldOfView;
        cinemachineCam.Lens.FieldOfView = Mathf.Lerp(current, target, fovSpeed * Time.deltaTime);
    }

    private void HandleHeadBob()
    {
        bool moving = _moveInput.magnitude > 0.1f;

        if (_characterController.isGrounded && moving)
        {
            _bobTimer += Time.deltaTime * bobFrequency
                      * (_isSprinting ? 1.4f : 1f); // faster bob while sprinting

            _bobOffset = new Vector3(
                Mathf.Sin(_bobTimer * 0.5f) * bobAmplitude * 0.5f, Mathf.Abs(Mathf.Sin(_bobTimer)) * bobAmplitude, 0f);
        }
        else
        {
            _bobTimer = 0f;

            _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero, Time.deltaTime * 12f);
            if (_bobOffset.magnitude < 0.001f) _bobOffset = Vector3.zero;
        }
        cameraBobTarget.localPosition = _bobTargetDefaultPos + _bobOffset;
    }

    void OnEnable()
    {
        _inputActions.Player.Enable();
        _inputActions.Player.Move.performed += callBackContext => _moveInput = callBackContext.ReadValue<Vector2>();
        _inputActions.Player.Move.canceled += callBackContext => _moveInput = Vector2.zero;
        _inputActions.Player.Look.performed += callBackContext => _lookInput = callBackContext.ReadValue<Vector2>();
        _inputActions.Player.Look.canceled += callBackContext => _lookInput = Vector2.zero;
        _inputActions.Player.Jump.performed += callBackContext => _jumpPressed = true;
        _inputActions.Player.Sprint.performed += callBackContext => _isSprinting = true;
        _inputActions.Player.Sprint.canceled += callBackContext => _isSprinting = false;
        _inputActions.Player.Crouch.performed += callBackContext => ToggleCrouch();
    }

    void OnDisable()
    {
        _inputActions.Player.Disable();
    }

}
