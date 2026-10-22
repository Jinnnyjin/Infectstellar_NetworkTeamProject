using DG.Tweening;
using UnityEngine;

/// <summary>
/// 베이스캠프의 두 문을 지정된 열림·닫힘 지점으로 함께 이동합니다.
/// </summary>
public class BaseCampDoor : MonoBehaviour
{
    [SerializeField] private Transform door42;
    [SerializeField] private Transform door43;
    [SerializeField] private Transform door42ClosePoint;
    [SerializeField] private Transform door42OpenPoint;
    [SerializeField] private Transform door43ClosePoint;
    [SerializeField] private Transform door43OpenPoint;
    [SerializeField, Min(0f), Tooltip("호출 시 현재 위치에서 목표 지점까지 이동하는 시간(초)")]
    private float moveDuration = 1f;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    private Sequence moveSequence;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// 객체 파괴 시 진행 중인 문 이동을 정리합니다.
    /// </summary>
    private void OnDestroy()
    {
        moveSequence?.Kill(false);
    }

    /// <summary>
    /// 진행 중인 이동을 중지하고 두 문을 엽니다.
    /// </summary>
    [ContextMenu("오픈")]
    public void Open()
    {
        audioSource.PlayOneShot(openSound);
        MoveDoors(door42OpenPoint, door43OpenPoint);
    }

    /// <summary>
    /// 진행 중인 이동을 중지하고 두 문을 닫습니다.
    /// </summary>
    [ContextMenu("클로즈")]
    public void Close()
    {
        audioSource.PlayOneShot(closeSound);
        MoveDoors(door42ClosePoint, door43ClosePoint);
    }

    /// <summary>
    /// 두 문을 현재 위치에서 각각의 목표 지점까지 동시에 이동합니다.
    /// </summary>
    private void MoveDoors(Transform target42, Transform target43)
    {
        moveSequence?.Kill(false);
        moveSequence = DOTween.Sequence().SetEase(Ease.Linear);
        moveSequence.Join(door42.DOMove(target42.position, moveDuration).SetEase(Ease.Linear));
        moveSequence.Join(door43.DOMove(target43.position, moveDuration).SetEase(Ease.Linear));
    }
}
