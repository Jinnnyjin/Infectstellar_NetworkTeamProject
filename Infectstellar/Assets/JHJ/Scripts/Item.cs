using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Item : MonoBehaviour
{
    public enum State { Placed, Grabbed, Thrown }
    public State CurrentState { get; private set; }

    [SerializeField] private ItemDataSO data;

    private Rigidbody rb;


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if(data == null)
        {
            Debug.LogError($"{gameObject.name} ItemDataSO가 연결되지 않음", this);
            gameObject.SetActive(false);
            return;
        }
        rb.mass = data.Weight;
        CurrentState = State.Placed;
    }
}
