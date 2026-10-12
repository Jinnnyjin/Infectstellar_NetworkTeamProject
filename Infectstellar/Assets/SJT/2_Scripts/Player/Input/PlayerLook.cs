using DG.Tweening;
using UnityEngine;

/// <summary>
/// 마우스 시점 회전과 실제 웅크림에 따른 시점 높이 이동을 처리합니다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerLook : MonoBehaviour
{
    #region 인스펙터 설정값
    [SerializeField, Tooltip("Player 루트의 직계 자식인 시점 축. 몸과 다른 Transform을 연결")]
    private Transform viewPivot;
    [SerializeField, Min(0f), Tooltip("마우스 X 이동 민감도")]
    private float horizontalSensitivity = 0.1f;
    [SerializeField, Min(0f), Tooltip("마우스 Y 이동 민감도")]
    private float verticalSensitivity = 0.1f;
    [SerializeField, Range(-89f, 0), Tooltip("위 쪽을 볼 때 허용하는 각도")]
    private float minPitch = -80f;
    [SerializeField, Range(89f, 0), Tooltip("위 쪽을 볼 때 허용하는 각도")]
    private float maxPitch = 80f;
    [SerializeField, Min(0f), Tooltip("웅크리기와 일어서기 시점 높이 전환 시간(초)")]
    private float heightTransitionDuration = 0.2f;
    #endregion

    private Transform body;
    private float yaw;
    private float pitch;
    private float standingViewHeight;
    private Tween heightTween;

    // 마우스로 확정한 상하 시선 각도. 위쪽은 음수, 아래쪽은 양수이며 단위는 도.
    public float PitchDegrees => pitch;

    private void Awake()
    {
        body = transform;
        if (viewPivot == null || viewPivot.parent != body)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerLook] Player 루트의 직계 자식인 ViewPivot을 연결해야 합니다.", this);
#endif
            enabled = false;
            return;
        }

        yaw = body.eulerAngles.y;
        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, viewPivot.localEulerAngles.x), minPitch, maxPitch);
        standingViewHeight = viewPivot.localPosition.y;
    }

    /// <summary>
    /// 재활성화 시 멈췄던 높이 전환을 이어갑니다.
    /// </summary>
    private void OnEnable() => heightTween?.Play();

    /// <summary>
    /// 비활성화 시 현재 시점 높이에서 전환을 멈춥니다.
    /// </summary>
    private void OnDisable() => heightTween?.Pause();

    /// <summary>
    /// 파괴 시 높이 전환을 정리합니다.
    /// </summary>
    private void OnDestroy() => heightTween?.Kill();

    /// <summary>
    /// 실제 캡슐이 줄어든 로컬 높이만큼 시점을 내립니다.
    /// </summary>
    public void SetCrouchHeight(float heightReduction)
    {
        if (viewPivot == null)
        {
            return;
        }

        heightTween?.Kill();
        heightTween = null;
        float targetHeight = standingViewHeight - heightReduction;
        if (heightTransitionDuration <= 0f)
        {
            Vector3 position = viewPivot.localPosition;
            position.y = targetHeight;
            viewPivot.localPosition = position;
            return;
        }

        heightTween = viewPivot.DOLocalMoveY(targetHeight, heightTransitionDuration)
            .SetEase(Ease.InOutSine)
            .SetAutoKill(false);
        if (!isActiveAndEnabled)
        {
            heightTween.Pause();
        }
    }

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
