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

    [Header("들기 (테스트)")] // [임시]
    [Tooltip("들고 있는 아이템이 따라갈 머리 위 소켓, 비우면 몸 자식으로 자동 생성")] // [임시]
    [SerializeField] private Transform headSocket; // [임시]
    [Tooltip("자동 생성되는 HeadSocket의 로컬 위치")] // [임시]
    [SerializeField] private Vector3 headSocketLocalPosition = new Vector3(0f, 2.2f, 0f); // [임시]

    private CharacterController controller;
    private float pitch;
    private float verticalVelocity;
    private IInteractable currentTarget;
    private Item heldItem; // [임시]


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

        // [임시] 머리 위 소켓: 카메라가 아닌 몸 자식 (좌우 회전만 따라감)
        if (headSocket == null) // [임시]
        {
            headSocket = new GameObject("HeadSocket").transform; // [임시]
            headSocket.SetParent(transform, false); // [임시]
            headSocket.localPosition = headSocketLocalPosition; // [임시]
            headSocket.localRotation = Quaternion.identity; // [임시]
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

        if (keyboard.fKey.wasPressedThisFrame) // [임시]
        {
            TryThrow(); // [임시]
        }
    }

    // [임시] 들고 있는 아이템을 머리 위 소켓에 붙여두기
    private void LateUpdate()
    {
        if (heldItem == null) return; // [임시]

        // [임시] 던진 뒤 다시 끌어오지 않도록 상태 확인이 먼저
        if (heldItem.CurrentState != Item.State.Grabbed) // [임시]
        {
            Debug.Log($"[DummyPlayer] 따라가기 해제: {heldItem.GetInfo().ItemName} ({heldItem.CurrentState})"); // [임시]
            heldItem = null; // [임시]
            return; // [임시]
        }

        heldItem.transform.SetPositionAndRotation(headSocket.position, headSocket.rotation); // [임시]
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
        if (heldItem != null) // [임시]
        {
            Debug.Log($"[DummyPlayer] E: 이미 들고 있음 - {heldItem.GetInfo().ItemName}"); // [임시]
            return; // [임시]
        }
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

        // [임시] 잡기 성공(Grabbed)일 때만 보관
        Item item = (currentTarget as Component)?.GetComponentInParent<Item>(); // [임시]
        if (item != null && item.CurrentState == Item.State.Grabbed) // [임시]
        {
            heldItem = item; // [임시]
            Debug.Log($"[DummyPlayer] 잡기 성공: {heldItem.GetInfo().ItemName} ({heldItem.CurrentState})"); // [임시]
        }
    }

    // [임시] F: 던지기 (heldItem 정리는 LateUpdate가 상태를 보고 처리)
    private void TryThrow()
    {
        if (heldItem == null) // [임시]
        {
            Debug.Log("[DummyPlayer] F: 들고 있는 아이템 없음"); // [임시]
            return; // [임시]
        }
        Debug.Log($"[DummyPlayer] 던지기 호출: {heldItem.GetInfo().ItemName} ({heldItem.CurrentState})"); // [임시]
        heldItem.Shoot(cameraPivot.forward); // [임시]
    }

    // =============================================================
    // 테스트용 화면 표시 (UI 세팅 없이 OnGUI 사용)

    private void OnGUI()
    {
        // 크로스헤어
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        GUI.Label(new Rect(cx - 5f, cy - 10f, 20f, 20f), "+");

        // [임시] 들고 있는 아이템 표시 (왼쪽 위)
        if (heldItem != null) // [임시]
        {
            GUI.Box(new Rect(10f, 10f, 220f, 45f),
                $"들고 있음: {heldItem.GetInfo().ItemName} ({heldItem.CurrentState})\n[F] 던지기"); // [임시]
        }

        if (currentTarget == null) return;

        InteractableInfo info = currentTarget.GetInfo();
        string grabText = currentTarget.CanGrab() ? "[E] 잡기" : "잡기 불가";
        GUI.Box(new Rect(cx + 20f, cy + 20f, 180f, 60f),
            $"{info.ItemName}\n가격: {info.Price}\n{grabText}");
    }

    private void OnDrawGizmosSelected()
    {
        // [임시] 머리 위 소켓 위치 (플레이 전엔 자동 생성 예정 위치)
        Vector3 socketPos = headSocket != null ? headSocket.position : transform.TransformPoint(headSocketLocalPosition); // [임시]
        Gizmos.color = Color.cyan; // [임시]
        Gizmos.DrawWireSphere(socketPos, 0.15f); // [임시]

        if (cameraPivot == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(cameraPivot.position, cameraPivot.forward * interactDistance);
    }
}
