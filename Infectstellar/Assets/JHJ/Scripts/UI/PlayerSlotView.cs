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

    private Player player;

    private void Awake()
    {
        if (playerIcon == null || playerNickname == null)
        {
            Debug.LogError($"[PlayerSlotView] 참조 미연결: {name}", this);
        }
    }


    public void SetInfo(PlayerStatusInfo info)
    {
        gameObject.SetActive(true);
        playerNickname.text = info.Nickname;


        playerIcon.color = info.IsAlive ? info.Color : Color.Lerp(info.Color, downMixColor, downMixRatio);
        playerNickname.color = info.IsAlive ? aliveColor : downColor;
    }

    public void Clear()
    {
        gameObject.SetActive(false);
    }


    // 테스트 코드

    [ContextMenu("Test/Alive")]
    private void TestAlive() => SetInfo(new PlayerStatusInfo("Test", true, Color.red));

    [ContextMenu("Test/Down")]
    private void TestDown() => SetInfo(new PlayerStatusInfo("Test", false, Color.red));

    [ContextMenu("Test/Clear")]
    private void TestClear() => Clear();
}
