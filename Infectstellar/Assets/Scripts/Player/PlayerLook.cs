using UnityEngine;

/// <summary>
/// 마우스의 좌우 입력은 몸에, 상하 입력은 독립 시점 축에 적용합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerLook : MonoBehaviour
{
    [SerializeField, Tooltip("Player 루트의 직계 자식인 시점 축입니다. 몸과 다른 Transform을 연결합니다.")]
    private Transform viewPivot;
    [SerializeField, Min(0f), Tooltip("마우스 X 이동 1픽셀당 몸의 회전 각도입니다.")]
    private float horizontalSensitivity = 0.1f;
    [SerializeField, Min(0f), Tooltip("마우스 Y 이동 1픽셀당 시점의 회전 각도입니다.")]
    private float verticalSensitivity = 0.1f;
    [SerializeField, Range(-89f, 0f), Tooltip("위쪽을 볼 때 허용하는 pitch의 최솟값입니다. 단위: 도.")]
    private float minPitch = -80f;
    [SerializeField, Range(0f, 89f), Tooltip("아래쪽을 볼 때 허용하는 pitch의 최댓값입니다. 단위: 도.")]
    private float maxPitch = 80f;

    private Transform body;
    private float yaw;
    private float pitch;

    /// <summary>
    /// 별도의 시점 축을 확인하고 현재 배치의 시작 각도를 보관합니다.
    /// </summary>
    private void Awake()
    {
        body = transform;
        if (viewPivot == null || viewPivot.parent != body)
        {
            Debug.LogError("[PlayerLook] Player 루트의 직계 자식인 ViewPivot을 연결해야 합니다.", this);
            enabled = false;
            return;
        }

        yaw = body.eulerAngles.y;
        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, viewPivot.localEulerAngles.x), minPitch, maxPitch);
    }

    /// <summary>
    /// 프레임당 마우스 이동량을 보간 없이 적용합니다. deltaTime은 곱하지 않습니다.
    /// </summary>
    public void Rotate(Vector2 lookDelta)
    {
        if (!isActiveAndEnabled || viewPivot == null)
        {
            return;
        }

        yaw = Mathf.Repeat(yaw + lookDelta.x * horizontalSensitivity, 360f);
        pitch = Mathf.Clamp(pitch - lookDelta.y * verticalSensitivity, minPitch, maxPitch);
        body.rotation = Quaternion.Euler(0f, yaw, 0f);
        viewPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
