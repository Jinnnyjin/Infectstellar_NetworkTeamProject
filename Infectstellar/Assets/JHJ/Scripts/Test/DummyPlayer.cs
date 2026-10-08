using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 아이템 상호작용 테스트용 더미 플레이어 (네트워크 X)
/// 마우스 좌우 = 몸(Yaw) 회전, 위아래 = 카메라(Pitch)만 회전
/// 구조: Player(CharacterController + DummyPlayer) / CameraPivot(Camera)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class DummyPlayer : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("위아래 회전을 받을 카메라 Transform, 비우면 자식 Camera 자동 탐색")]
    [SerializeField] private Transform cameraPivot;

    [Header("이동")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float gravity = -9.81f;

    [Header("시점")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("레이캐스트")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactMask = ~0;

    private CharacterController controller;
    private float pitch;
    private float verticalVelocity;
    private IInteractable currentTarget;


    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraPivot == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam == null)
            {
                Debug.LogError($"{gameObject.name} 자식 Camera가 없음", this);
                enabled = false;
                return;
            }
            cameraPivot = cam.transform;
        }
    }

    private void OnEnable()
    {
        LockCursor(true);
    }

    private void OnDisable()
    {
        LockCursor(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        HandleCursor(keyboard, mouse);
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Look(mouse);
        }
        Move(keyboard);
        UpdateTarget();

        if (keyboard.eKey.wasPressedThisFrame)
        {
            TryGrab();
        }
    }

    // =============================================================
    // 입력

    private void HandleCursor(Keyboard keyboard, Mouse mouse)
    {
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            LockCursor(false);
        }
        else if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor(true);
        }
    }

    private void LockCursor(bool isLocked)
    {
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }

    private void Look(Mouse mouse)
    {
        Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;

        // 좌우: 몸 전체
        transform.Rotate(Vector3.up, delta.x);

        // 위아래: 카메라만
        pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Move(Keyboard keyboard)
    {
        Vector2 input = Vector2.zero;
        if (keyboard.wKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;
        if (keyboard.aKey.isPressed) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 move = (transform.right * input.x + transform.forward * input.y) * moveSpeed;

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f; // 바닥에 붙여두기
        }
        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }

    // =============================================================
    // 상호작용

    private void UpdateTarget()
    {
        currentTarget = null;

        Ray ray = new Ray(cameraPivot.position, cameraPivot.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Ignore))
        {
            // 콜라이더가 자식에 붙어있는 경우 대비
            currentTarget = hit.collider.GetComponentInParent<IInteractable>();
        }
    }

    private void TryGrab()
    {
        if (currentTarget == null)
        {
            Debug.Log("[DummyPlayer] E: 바라보는 대상 없음");
            return;
        }
        if (!currentTarget.CanGrab())
        {
            Debug.Log($"[DummyPlayer] E: Grab 불가 - {currentTarget.GetInfo().ItemName}");
            return;
        }
        currentTarget.OnGrab();
    }

    // =============================================================
    // 테스트용 화면 표시 (UI 세팅 없이 OnGUI 사용)

    private void OnGUI()
    {
        // 크로스헤어
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        GUI.Label(new Rect(cx - 5f, cy - 10f, 20f, 20f), "+");

        if (currentTarget == null) return;

        InteractableInfo info = currentTarget.GetInfo();
        string grabText = currentTarget.CanGrab() ? "[E] 잡기" : "잡기 불가";
        GUI.Box(new Rect(cx + 20f, cy + 20f, 180f, 60f),
            $"{info.ItemName}\n가격: {info.Price}\n{grabText}");
    }

    private void OnDrawGizmosSelected()
    {
        if (cameraPivot == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(cameraPivot.position, cameraPivot.forward * interactDistance);
    }
}
