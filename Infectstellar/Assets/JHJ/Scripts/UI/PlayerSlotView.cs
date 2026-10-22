using ENZEUN.Runtime;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSlotView : MonoBehaviour
{
    [SerializeField] private Image playerIcon;
    [SerializeField] private TMP_Text playerNickname;

    [Header("닉네임 색")]
    [SerializeField] private Color aliveColor = Color.white;
    [SerializeField] private Color downColor = Color.gray3;

    [Header("쓰러짐")]
    [SerializeField] private Color downMixColor = Color.gray3;
    [SerializeField, Range(0f, 1f)] private float downMixRatio = 0.6f;

    public Player player;
    public PlayerDataObserver playerDataObserver;
    private Color myColor;


    private void Awake()
    {
        if (playerIcon == null || playerNickname == null)
        {
            Debug.LogError($"[PlayerSlotView] 참조 미연결: {name}", this);
        }
    }

    public void OnDisable()
    {
        if (playerDataObserver != null)
        {
            playerDataObserver.OnPlayerDie -= RefreshColor;
        }
    }


    public void SetInfo(PlayerDataObserver info)
    {
        playerDataObserver = info;
        player = info.ownerPlayer;

        playerDataObserver.OnPlayerDie += RefreshColor;

        gameObject.SetActive(true);
        playerNickname.text = player.NickName;

        Color newColor;

        if (player.CustomProperties.TryGetValue(PhotonLinkManager.myColor_Customproperty, out object value) && value is int)
        {
            newColor = PhotonLinkManager.GetIndexedColor((int)value);
        }
        else
        {
            Debug.LogWarning($"[PlayerSlotView] 플레이어[{player.NickName}] 의 색 정보 없음, 흰색으로 표시");
            newColor = Color.white;
        }

        myColor = newColor;

        RefreshColor();
    }

    public void RefreshColor()
    {
        playerIcon.color = playerDataObserver.isDead ? Color.Lerp(myColor, downMixColor, downMixRatio) : myColor;
        playerNickname.color = playerDataObserver.isDead ? downColor : aliveColor;
    }

    public void Clear()
    {
        gameObject.SetActive(false);
    }


    // 테스트 코드

    // [ContextMenu("Test/Alive")]
    // private void TestAlive() => SetInfo(new PlayerStatusInfo("Test", true, Color.red));

    // [ContextMenu("Test/Down")]
    // private void TestDown() => SetInfo(new PlayerStatusInfo("Test", false, Color.red));

    // [ContextMenu("Test/Clear")]
    // private void TestClear() => Clear();
}
