using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum ParkourState
{
    None,
    Vaulting,
    Climbing,
    WallRunning,
    LedgeGrab,
}
// None, normal locomotion, all checks active
// Vaulting, normal locomotion, all checks active
// Climbing, pull-up over tall obstacle
// Wallrunning, running along a vertical wall
// LedgeGrab, hanging from a ledge edge

public class ParkourController : MonoBehaviour
{
    public float detectRange = 1.2f;
    public float vaultMaxHeight = 1.1f;
    public float climbMaxHeight = 2.2f;
    public float vaultDuration = 0.35f;
    public float climbDuration = 0.6f;
    public float ledgeGrabReach = 0.6f;
    public float hangForwardOffset = 0.3f;

    public LayerMask climbableMask;

    public ParkourState State {  get; private set; } = ParkourState.None;
    public bool IsParkouring {  get; private set; }

    private PlayerController _playerController;
    private CharacterController _characterController;
    private Animator _animator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _playerController = GetComponent<PlayerController>();
        _characterController = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if (State != ParkourState.None) return;

        CheckWallRun();
        CheckLedgeGrab();

        if(_playerController.MoveInput.y > 0.3f)
        {
            CheckVaultOrClimb();
        }
    }

    private void SetState(ParkourState next)
    {
        State = next;
        _playerController.enabled = (next == ParkourState.None);
    }

    private void CheckParkour()
    {
        // 1. Wall check
        bool wallAhead = Physics.Raycast(transform.position + Vector3.up, transform.forward, out RaycastHit wallHit, 1.2f);
        // 2. Top edge clear?
        bool topClear = !Physics.Raycast(transform.position + Vector3.up * 2.2f, transform.forward, 1.2f);
        // 3. Ledge surface
        bool ledgeTop = Physics.Raycast(transform.position + transform.forward * 1.2f + Vector3.up * 2.5f, Vector3.down, out RaycastHit ledgeHit, 1f);

        if (wallAhead && topClear && ledgeTop)
        {
            if (wallHit.collider.CompareTag("Climbable"))
                StartCoroutine(VaultOrClimb(ledgeHit.point));
        }
    }

    private void CheckWallRun()
    {

    }
    private void CheckLedgeGrab()
    {
        if (_characterController.isGrounded) return;
        if (_playerController.VerticalVelocity >= 0) return; // still rising

        Vector3 probe = transform.position
                      + Vector3.up * ledgeGrabReach
                      + transform.forward * hangForwardOffset;

        bool ledge = Physics.Raycast(
            probe, Vector3.down,
            out RaycastHit hit, 0.4f, climbableMask);

        if (ledge) StartCoroutine(DoLedgeGrab(hit.point));
    }
    IEnumerator DoLedgeGrab(Vector3 ledgePoint)
    {
        SetState(ParkourState.LedgeGrab);
        _animator.CrossFade("LedgeHang", 0.15f);

        // Snap so player's hands sit at ledge height
        _characterController.enabled = false;
        transform.position = new Vector3(
            transform.position.x,
            ledgePoint.y - ledgeGrabReach,
            transform.position.z)
            + transform.forward * hangForwardOffset;

        // Hang until Jump is pressed
        while (!_playerController.InputActions.Player.Jump.WasPressedThisFrame())
            yield return null;

        _characterController.enabled = true;
        // Reuse DoClimb to get on top of the ledge
        yield return StartCoroutine(
            DoClimb(ledgePoint + transform.forward * 0.5f));
    }
    private void CheckVaultOrClimb()
    {
        Vector3 origin = transform.position + Vector3.up * 0.8f;

        // Ray 1: wall directly ahead?
        bool wallAhead = Physics.Raycast(origin, transform.forward, out RaycastHit wallHit, detectRange, climbableMask);
        if (!wallAhead) return;

        // Ray 2: is the top clear at vault height?
        Vector3 vaultCheck = transform.position + Vector3.up * (vaultMaxHeight + 0.1f);
        bool topClear = !Physics.Raycast(vaultCheck, transform.forward, detectRange, climbableMask);

        // Ray 3: is there a surface on top to land on?
        Vector3 ledgeProbe = transform.position + transform.forward * (detectRange + 0.1f) + Vector3.up * (climbMaxHeight + 0.2f);
        bool hasSurface = Physics.Raycast(ledgeProbe, Vector3.down, out RaycastHit ledgeHit, climbMaxHeight, climbableMask);
        if (!hasSurface) return;

        // Decision: vault if top is clear, else climb
        if (topClear)
            StartCoroutine(DoVault(ledgeHit.point));
        else
            StartCoroutine(DoClimb(ledgeHit.point));
    }

    IEnumerator DoVault(Vector3 landPos)
    {
        SetState(ParkourState.Vaulting);
        _animator.CrossFade("Vault", 0.1f);

        Vector3 start = transform.position;
        // Arc: start -> midpoint above obstacle -> land
        Vector3 mid = (start + landPos) * 0.5f + Vector3.up * 0.6f;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / vaultDuration;
            t = Mathf.Clamp01(t);

            // Quadratic Bezier: lerp(lerp(start,mid,t), lerp(mid,land,t), t)
            Vector3 a = Vector3.Lerp(start, mid, t);
            Vector3 b = Vector3.Lerp(mid, landPos, t);

            _characterController.enabled = false;
            transform.position = Vector3.Lerp(a, b, t);
            _characterController.enabled = true;
            yield return null;
        }

        transform.position = landPos;
        SetState(ParkourState.None);
        // Always re-enable CharacterController to avoid player being stuck
        if (!_characterController.enabled) { _characterController.enabled = true; }
    }

    IEnumerator DoClimb(Vector3 landPos)
    {
        SetState(ParkourState.Climbing);
        _animator.CrossFade("Climb", 0.1f);

        Vector3 start = transform.position;
        // Phase 1 target: directly above current pos at ledge height
        Vector3 pullUp = new Vector3(
            start.x, landPos.y + 0.1f, start.z);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / climbDuration;
            t = Mathf.Clamp01(t);

            Vector3 pos = t < 0.5f
                // Phase 1: rise straight up (t 0->0.5 remapped to 0->1)
                ? Vector3.Lerp(start, pullUp, t * 2f)
                // Phase 2: step forward onto surface (t 0.5->1 remapped to 0->1)
                : Vector3.Lerp(pullUp, landPos, (t - 0.5f) * 2f);

            _characterController.enabled = false;
            transform.position = pos;
            _characterController.enabled = true;
            yield return null;
        }
        transform.position = landPos;
        SetState(ParkourState.None);
        if (!_characterController.enabled) { _characterController.enabled = true; }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.8f, transform.forward * detectRange);
    }

    private IEnumerator VaultOrClimb(Vector3 targetPosition)
    {
        IsParkouring = true;
        float elapsed = 0f, duration = 0.5f;
        Vector3 start = transform.position;
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(start, targetPosition + Vector3.up, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        IsParkouring = false;
    }

}
