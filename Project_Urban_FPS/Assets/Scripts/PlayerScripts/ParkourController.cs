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

    }
    private void CheckVaultOrClimb()
    {

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
