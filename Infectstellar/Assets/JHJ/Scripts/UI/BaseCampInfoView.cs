using TMPro;
using UnityEngine;
using ENZEUN.Runtime;
using Sirenix.OdinInspector;
using MoreMountains.Feedbacks;

/// <summary>
/// 게임매니저에서 초기값도 준다고 생각하고 구현, 추후 수정될 수 있음 [임시]
/// </summary>
public class BaseCampInfoView : MonoBehaviour
{
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private PlayerSlotView[] playerSlots;
    [SerializeField] private TMP_Text noneInfectedText;
    [SerializeField] private TMP_Text infectedText;
    [BoxGroup("FeedBacks")]
    [SerializeField] private MMF_Player goldFb;
    [BoxGroup("FeedBacks")]
    [SerializeField] private MMF_Player infectFb;

    private GameManager gameManagerRef;

    [Button]
    public void InitializeBaseCampInfo()
    {
        gameManagerRef = GameManager.Instance;

        if (gameManagerRef == null)
        {
            Debug.Log("게임매니저가 초기화되지 않았습니다. 베이스캠프 정보를 불러올 수 없습니다.");
            return;
        }

        int count = 0;

        foreach (var pair in gameManagerRef.playerDataDict.Values)
        {
            playerSlots[count].SetInfo(pair);
            count++;
        }

        foreach (var slot in playerSlots)
        {
            if (slot.player == null)
            {
                slot.Clear();
            }
        }

        ShowGold();
        ShowInfected();

        gameManagerRef.OnGoldChanged += ShowGold;
        gameManagerRef.OnPlayerInfected += ShowInfected;
        gameManagerRef.OnInfectedStateChanged += ShowInfected;
    }

    public void OnDisable()
    {
        gameManagerRef.OnGoldChanged -= ShowGold;
        gameManagerRef.OnPlayerInfected -= ShowInfected;
        gameManagerRef.OnInfectedStateChanged -= ShowInfected;
    }

    public void Start()
    {
        if (GameManager.Instance != null)
        {
            InitializeBaseCampInfo();

        }
        else
            Debug.Log("게임매니저가 초기화되지 않았습니다. 베이스캠프 정보를 불러올 수 없습니다.");
    }

    private void Awake()
    {
        if (goldText == null || noneInfectedText == null || infectedText == null)
            Debug.LogError($"[BaseCampInfoView] 참조 미연결: {name}", this);

        if (playerSlots == null || playerSlots.Length == 0)
        {
            Debug.LogError($"[BaseCampInfoView] 플레이어 슬롯 미연결: {name}", this);
            return;
        }

        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] == null)
                Debug.LogError($"[BaseCampInfoView] 플레이어 슬롯 {i}번 비어 있음: {name}", this);
        }
    }

    /// <summary>
    /// 게임매니저? 에서 골드가 바뀌는 이벤트 구독 예정
    /// </summary>
    public void ShowGold()
    {
        int amount = gameManagerRef.currentGold;
        goldText.text = amount.ToString("N0") + " G";
        goldFb?.PlayFeedbacks();
    }

    [Button]
    public void ShowInfected()
    {
        bool infected = gameManagerRef.isInfected;

        infectedText.gameObject.SetActive(infected);
        noneInfectedText.gameObject.SetActive(!infected);

        if (infected)
        {
            infectFb?.PlayFeedbacks();
        }
        else
        {
            infectFb?.StopFeedbacks();
        }
    }


}