using UnityEngine;

/// <summary>
/// 상호작용 안내 문구 모음
/// 에셋은 Resources 폴더에 "InteractPrompt" 이름으로 1개만
/// </summary>
[CreateAssetMenu(menuName = "UI/Interact Prompt")]
public class InteractPromptSO : ScriptableObject
{
    private const string RESOURCE_PATH = "InteractPrompt";

    [Header("아이템")]
    [SerializeField] private string grab = "[E] 잡기";
    [SerializeField] private string cannotGrab = "잡기 불가";
    [SerializeField, TextArea] private string held = "[E] 놓기\n[좌클릭] 던지기";

    [Header("상점")]
    [SerializeField, Tooltip("{0} = 아이템 이름, {1} = 가격")] private string purchaseFormat = "[E] 구매 {0} ({1} G)";
    [SerializeField] private string cannotPurchase = "구매 불가";

    [Header("문")]
    [SerializeField] private string doorOpen = "[E] 문 열기";
    [SerializeField] private string doorClose = "[E] 문 닫기";

    [Header("탈출")]
    [SerializeField] private string escape = "[E] 탈출";
    [SerializeField] private string escapeBlocked = "문을 닫아야 탈출 가능";

    public string Grab => grab;
    public string CannotGrab => cannotGrab;
    public string Held => held;
    public string CannotPurchase => cannotPurchase;
    public string DoorOpen => doorOpen;
    public string DoorClose => doorClose;
    public string Escape => escape;
    public string EscapeBlocked => escapeBlocked;

    public string GetPurchaseText(string itemName, int price)
    {
        return string.Format(purchaseFormat, itemName, price);
    }

    // =============================================================

    private static InteractPromptSO instance;

    /// <summary>
    /// Resources에서 처음 한 번 불러옴
    /// 에셋이 없으면 코드 기본값으로 임시 생성 (null 아님)
    /// </summary>
    public static InteractPromptSO Instance
    {
        get
        {
            if (instance != null)
                return instance;

            instance = Resources.Load<InteractPromptSO>(RESOURCE_PATH);
            if (instance == null)
            {
                Debug.LogError($"[InteractPromptSO] Resources/{RESOURCE_PATH} 에셋 없음, 기본 문구 사용");
                instance = CreateInstance<InteractPromptSO>();
            }

            return instance;
        }
    }
}
