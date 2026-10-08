using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Item : MonoBehaviour, IInteractable
{
    public enum State { Placed, Grabbed, Thrown }
    public State CurrentState { get; private set; }
    public ItemDataSO ItemData => data;

    [SerializeField] private ItemDataSO data;

    private Rigidbody rb;
    private Collider[] colliders;
    private float uprightBottomOffset;


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if(data == null)
        {
            Debug.LogError($"{gameObject.name} ItemDataSO가 연결되지 않음", this);
            gameObject.SetActive(false);
            return;
        }

        // 자식 콜라이더 전부 저장 
        colliders = GetComponentsInChildren<Collider>();

        if(!ColliderBoundsUtil.TryGetUprightBottomOffset(transform, colliders, out uprightBottomOffset))
        {
            Debug.LogError($"{gameObject.name} 최하단 오프셋 구하기 실패", this);
            gameObject.SetActive(false);
            return;
        }

        rb.mass = data.Weight;
        CurrentState = State.Placed;
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
            Debug.LogWarning($"[Item] Grab 불가 :{data.ItemName}, 상태 {CurrentState}", this);
            return;
        }

        Debug.Log($"[Item] Grab: {data.ItemName}, 상태 {CurrentState}", this);
    }
}
