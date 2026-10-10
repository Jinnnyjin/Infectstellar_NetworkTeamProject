using UnityEngine;

/// <summary>
/// Player의 클립과 기본 Animator 구조, 공통 전환 시간을 보관합니다.
/// </summary>
[CreateAssetMenu(fileName = "PlayerAnimationSettings", menuName = "Player/Animation Settings")]
public sealed class PlayerAnimationSettings : ScriptableObject
{
    [SerializeField] private RuntimeAnimatorController baseController;

    [Header("정지")]
    [SerializeField, InspectorName("서 있기")] private AnimationClip idle;
    [SerializeField, InspectorName("웅크린 채 서 있기")] private AnimationClip crouchIdle;

    [Header("걷기")]
    [SerializeField, InspectorName("앞으로 걷기")] private AnimationClip walkForward;
    [SerializeField, InspectorName("뒤로 걷기")] private AnimationClip walkBackward;
    [SerializeField, InspectorName("왼쪽으로 걷기")] private AnimationClip walkLeft;
    [SerializeField, InspectorName("오른쪽으로 걷기")] private AnimationClip walkRight;

    [Header("달리기")]
    [SerializeField, InspectorName("앞으로 달리기")] private AnimationClip runForward;
    [SerializeField, InspectorName("뒤로 달리기")] private AnimationClip runBackward;
    [SerializeField, InspectorName("왼쪽으로 달리기")] private AnimationClip runLeft;
    [SerializeField, InspectorName("오른쪽으로 달리기")] private AnimationClip runRight;

    [Header("웅크린 걷기")]
    [SerializeField, InspectorName("웅크려 앞으로 걷기")] private AnimationClip crouchWalkForward;
    [SerializeField, InspectorName("웅크려 뒤로 걷기")] private AnimationClip crouchWalkBackward;
    [SerializeField, InspectorName("웅크려 왼쪽으로 걷기")] private AnimationClip crouchWalkLeft;
    [SerializeField, InspectorName("웅크려 오른쪽으로 걷기")] private AnimationClip crouchWalkRight;

    [Header("공중·사망")]
    [SerializeField, InspectorName("점프 상승")] private AnimationClip jump;
    [SerializeField, InspectorName("하강")] private AnimationClip fall;
    [SerializeField, InspectorName("사망")] private AnimationClip death;

    [SerializeField, Min(0f), Tooltip("상태 전환과 방향 입력 완화에 함께 사용하는 시간(초)입니다.")] private float stateBlendDuration = 0.15f;

    public RuntimeAnimatorController BaseController => baseController;
    public float BlendDuration => float.IsNaN(stateBlendDuration) || float.IsInfinity(stateBlendDuration) ? 0.15f : Mathf.Max(0f, stateBlendDuration);

    /// <summary>
    /// 기본 Controller의 슬롯 이름으로 클립을 찾습니다. 비어 있는 슬롯은 Idle로 대체합니다.
    /// </summary>
    internal AnimationClip GetClip(string slotName)
    {
        return slotName switch
        {
            nameof(idle) => idle,
            nameof(crouchIdle) => crouchIdle != null ? crouchIdle : idle,
            nameof(walkForward) => walkForward != null ? walkForward : idle,
            nameof(walkBackward) => walkBackward != null ? walkBackward : idle,
            nameof(walkLeft) => walkLeft != null ? walkLeft : idle,
            nameof(walkRight) => walkRight != null ? walkRight : idle,
            nameof(runForward) => runForward != null ? runForward : idle,
            nameof(runBackward) => runBackward != null ? runBackward : idle,
            nameof(runLeft) => runLeft != null ? runLeft : idle,
            nameof(runRight) => runRight != null ? runRight : idle,
            nameof(crouchWalkForward) => crouchWalkForward != null ? crouchWalkForward : idle,
            nameof(crouchWalkBackward) => crouchWalkBackward != null ? crouchWalkBackward : idle,
            nameof(crouchWalkLeft) => crouchWalkLeft != null ? crouchWalkLeft : idle,
            nameof(crouchWalkRight) => crouchWalkRight != null ? crouchWalkRight : idle,
            nameof(jump) => jump != null ? jump : idle,
            nameof(fall) => fall != null ? fall : idle,
            nameof(death) => death != null ? death : idle,
            _ => null
        };
    }
}
