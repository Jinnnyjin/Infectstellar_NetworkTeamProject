using Photon.Pun;
using System;
using UnityEngine;

/// <summary>
/// 플레이어가 바라보는 IInteractable 탐지
/// </summary>
[RequireComponent(typeof(PlayerStateMachine))] 
[DisallowMultipleComponent]
public class PlayerInteractor : MonoBehaviour
{
    public IInteractable CurrentTarget { get; private set; }
    public event Action<IInteractable> targetChanged;

    [SerializeField] private Transform viewPivot;
    [SerializeField, Min(0f)] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactMask = ~0;

    private PlayerStateMachine stateMachine;


    private void Awake()
    {
        PhotonView view = GetComponent<PhotonView>();

        // 방 안의 남의 플레이어만 끔 (PhotonView 없음, 방 밖 = 오프라인 테스트로 보고 진행)
        if (view != null && PhotonNetwork.InRoom && !view.IsMine)
        {
            enabled = false;
            return;
        }

        stateMachine = GetComponent<PlayerStateMachine>();

        if(viewPivot == null)
        {
            Debug.LogError($"[PlayerInteractor] {gameObject.name}: ViewPivot 연결되지 않음", this);
            enabled = false;
            return;
        }
    }

    private void OnDisable()
    {
        // 이전 대상을 계속 띄우지 않도록
        SetTarget(null);
    }

    /// <summary>
    /// 모든 Update 이후 최종 시점 기준 탐지
    /// </summary>
    private void LateUpdate()
    {
        IInteractable newTarget = null;

        if(stateMachine.IsAlive)
        {
            newTarget = FindTarget();
        }

        SetTarget(newTarget);
    }

    /// <summary>
    /// ViewPivot 정면 레이 맞은 대상 IInteractable 반환 / 없으면 null
    /// </summary>
    private IInteractable FindTarget()
    {
        Ray ray = new Ray(viewPivot.position, viewPivot.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Ignore))
            return null;

        return hit.collider.GetComponentInParent<IInteractable>();
    }

    /// <summary>
    /// 현재 보는 대상 변경
    /// </summary>
    private void SetTarget(IInteractable newTarget)
    {
        if (newTarget == CurrentTarget)
            return;

        CurrentTarget = newTarget;
        targetChanged?.Invoke(CurrentTarget);
    }

    private void OnDrawGizmosSelected()
    {
        if (viewPivot == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(viewPivot.position, viewPivot.forward * interactDistance);
    }
}
