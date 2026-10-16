using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSlotView : MonoBehaviour
{
    [SerializeField] private Image playerIcon;
    [SerializeField] private TMP_Text playerNickname;

    [SerializeField] private Color aliveColor = Color.white;
    [SerializeField] private Color downColor = Color.gray3;


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

        Color color = info.IsAlive ? aliveColor : downColor;

        playerNickname.color = color;
        playerIcon.color = color;
    }

    public void Clear()
    {
        gameObject.SetActive(false);
    }

    [ContextMenu("Test/Alive")]
    private void TestAlive()
    {
        SetInfo(new PlayerStatusInfo("Test", true));
    }

    [ContextMenu("Test/Down")]
    private void TestDown()
    {
        SetInfo(new PlayerStatusInfo("Test", false));
    }

    [ContextMenu("Test/Clear")]
    private void TestClear()
    {
        Clear();
    }
}
