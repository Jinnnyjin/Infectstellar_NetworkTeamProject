using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractor))]
public class InteractInfoDisplay : MonoBehaviour
{
    private PlayerInteractor interactor;
    private IInteractable displayingTarget;
    private InteractableInfo targetInfo;

    private void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();
    }

    private void OnEnable()
    {
        interactor.targetChanged += HandleTargetChanged;
    }

    private void OnDisable()
    {
        interactor.targetChanged -= HandleTargetChanged;
        displayingTarget = null;
    }

    private void HandleTargetChanged(IInteractable newTarget)
    {
        displayingTarget = newTarget;
        if(newTarget != null)
        {
            targetInfo = newTarget.GetInfo();
        }
    }

    /// <summary>
    /// [임시] UI작업 시 교체 필요
    /// </summary>
    private void OnGUI()
    {
        if (!interactor.enabled)
            return;

        // 크로스헤어
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        GUI.Label(new Rect(cx - 5f, cy - 10f, 20f, 20f), "+");

        if (displayingTarget == null)
            return;

        string grabText = displayingTarget.CanGrab() ? "[E] 잡기" : "잡기 불가";
        GUI.Box(new Rect(cx + 20f, cy + 20f, 180f, 60f),
            $"{targetInfo.ItemName}\n가격: {targetInfo.Price}\n{grabText}");
    }
}
