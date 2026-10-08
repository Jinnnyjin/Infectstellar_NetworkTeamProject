using UnityEngine;

[RequireComponent(typeof(ItemPhysics))]
public class Item : MonoBehaviour, IInteractable
{
    public enum State { None, Placed, Grabbed, Thrown }
    public State CurrentState { get; private set; }
    public ItemDataSO ItemData => data;

    [SerializeField] private ItemDataSO data;

    private float uprightBottomOffset;
    private ItemPhysics physics;


    private void Awake()
    {
        if(data == null)
        {
            Debug.LogError($"{gameObject.name} ItemDataSO가 연결되지 않음", this);
            gameObject.SetActive(false);
            return;
        }

        // 아이템 물리 Init
        physics = GetComponent<ItemPhysics>();
        if(!physics.Init(data.Weight))
        {
            Debug.LogError($"{gameObject.name} 켜진 콜라이더가 없음", this);
            gameObject.SetActive(false);
            return;
        }

        // 최하단 오프셋 계산
        if (!ColliderBoundsUtil.TryGetUprightBottomOffset(transform, physics.Colliders, out uprightBottomOffset))
        {
            Debug.LogError($"{gameObject.name} 최하단 오프셋 구하기 실패", this);
            gameObject.SetActive(false);
            return;
        }

        ChangeState(State.Placed);
    }

    // =============================================================
    // 상태 전환
    private void ChangeState(State newState)
    {
        if(newState == CurrentState)
        {
            Debug.LogWarning($"[Item] {data.ItemName} 이미 {CurrentState} 상태, 전환 무시", this);
            return;
        }

        State prevState = CurrentState;
        CurrentState = newState;

        switch(newState)
        {
            case State.Placed:
                physics.SetPhysics(false);
                physics.SetColliders(true); // 레이캐스트 대상
                break;

            case State.Grabbed:
                physics.SetPhysics(false);
                physics.SetColliders(false); // 몸, 벽이랑 부딪히지 않게
                break;

            case State.Thrown:
                physics.SetColliders(true); 
                physics.SetPhysics(true);
                break;

            default:
                Debug.LogError($"[Item] {data.ItemName} 처리하지 않는 상태 {newState}", this);
                break;
        }

        Debug.Log($"[Item] {data.ItemName}: {prevState} → {newState}", this);
    }

    // =============================================================
    // 인터페이스

    public InteractableInfo GetInfo()
    {
        return new InteractableInfo(data.ItemName, data.Price);
    }

    public bool CanGrab()
    {
        return CurrentState == State.Placed;
    }

    public void OnGrab()
    {
        if (!CanGrab())
        {
            Debug.LogWarning($"[Item] OnGrab 불가 :{data.ItemName}, 상태 {CurrentState}", this);
            return;
        }

        ChangeState(State.Grabbed);
    }
}
