using UnityEngine;

/// <summary>
/// Player 애니메이션의 공통 전환 시간을 보관합니다. 클립은 Animator Controller에서 관리합니다.
/// </summary>
[CreateAssetMenu(fileName = "PlayerAnimationSettings", menuName = "Player/Animation Settings")]
public sealed class PlayerAnimationSettings : ScriptableObject
{
    [SerializeField, Min(0f), Tooltip("상태 전환과 방향 입력 완화에 함께 사용하는 시간(초)입니다.")] private float stateBlendDuration = 0.15f;

    /// <summary>
    /// 상태 전환과 방향 입력 완화에 사용할 유효한 시간(초)을 반환합니다.
    /// </summary>
    public float BlendDuration => float.IsNaN(stateBlendDuration) || float.IsInfinity(stateBlendDuration) ? 0.15f : Mathf.Max(0f, stateBlendDuration);
}
