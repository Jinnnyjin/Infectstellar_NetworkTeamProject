using MoreMountains.Feedbacks;
using UnityEngine;

public class FeedbackPlayer : MonoBehaviour
{
    [SerializeField] MMF_Player mmf_Player;

    [ContextMenu("Player Feedback")]
    public void PlayFeedback()
    {
        mmf_Player.PlayFeedbacks();
    }
}
