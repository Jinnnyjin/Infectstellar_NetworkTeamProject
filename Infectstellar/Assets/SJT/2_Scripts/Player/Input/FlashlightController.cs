using System;
using UnityEngine;

/// <summary>
/// 손전등과 해당 플레이어의 헬멧 색을 같은 켜짐 상태로 관리합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FlashlightController : MonoBehaviour
{
    private const string c_baseColorProperty = "_BaseColor";

    [SerializeField] private Light flashlightLight;
    [SerializeField, Tooltip("Player 모델의 SkinnedMeshRenderer를 연결")]
    private Renderer helmetRenderer;
    [SerializeField, Min(0), Tooltip("현재 Player_Helm은 모델의 Material 슬롯 2")]
    private int helmetMaterialIndex = 2;
    [SerializeField] private Color onColor = Color.white;
    [SerializeField] private Color offColor = Color.black;

    private MaterialPropertyBlock propertyBlock;
    private int baseColorId;
    private bool isOn;
    private bool isInitialized;

    /// <summary>
    /// 마지막으로 빛과 헬멧에 적용한 켜짐 상태입니다.
    /// </summary>
    public bool IsOn => isOn;

    /// <summary>
    /// 빛과 헬멧에 새 상태를 적용한 뒤 실제 변경이 있을 때 한 번 알립니다.
    /// </summary>
    public event Action<bool> D_FlashlightStateChanged;

    /// <summary>
    /// 필수 참조를 확인하고 최초 꺼짐 상태를 적용합니다.
    /// </summary>
    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// 재활성화할 때 보관한 상태를 빛과 헬멧에 다시 적용합니다.
    /// </summary>
    private void OnEnable()
    {
        if (EnsureInitialized() && CanApplyState())
        {
            ApplyState();
        }
    }

    /// <summary>
    /// 활성 상태에서 손전등의 현재 켜짐 여부를 반전합니다.
    /// </summary>
    public void ToggleFlashlight()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        SetFlashlightEnabled(!isOn);
    }

    /// <summary>
    /// 지정한 상태를 적용합니다. 비활성 중에도 사망 등의 명시적인 설정을 허용합니다.
    /// </summary>
    public void SetFlashlightEnabled(bool isOn)
    {
        if (!EnsureInitialized() || !CanApplyState() || this.isOn == isOn)
        {
            return;
        }

        this.isOn = isOn;
        ApplyState();
        D_FlashlightStateChanged?.Invoke(isOn);
    }

    /// <summary>
    /// Awake보다 먼저 설정 요청을 받아도 한 번만 참조를 검증하고 초기화합니다.
    /// </summary>
    private bool EnsureInitialized()
    {
        if (isInitialized)
        {
            return true;
        }

        if (!CanApplyState())
        {
            return false;
        }

        Material[] materials = helmetRenderer.sharedMaterials;
        baseColorId = Shader.PropertyToID(c_baseColorProperty);
        if (helmetMaterialIndex < 0 || helmetMaterialIndex >= materials.Length || materials[helmetMaterialIndex] == null || !materials[helmetMaterialIndex].HasProperty(baseColorId))
        {
#if UNITY_EDITOR
            Debug.LogError("[FlashlightController] 헬멧 Material 슬롯과 _BaseColor 속성을 확인해야 합니다.", this);
#endif
            enabled = false;
            return false;
        }

        propertyBlock = new MaterialPropertyBlock();
        isOn = false;
        isInitialized = true;
        ApplyState();
        return true;
    }

    /// <summary>
    /// 필수 객체가 없거나 파괴됐다면 이 컴포넌트의 처리를 중지합니다.
    /// </summary>
    private bool CanApplyState()
    {
        if (flashlightLight != null && helmetRenderer != null)
        {
            return true;
        }

#if UNITY_EDITOR
        Debug.LogError("[FlashlightController] 손전등 Light와 헬멧 Renderer를 연결해야 합니다.", this);
#endif
        enabled = false;
        return false;
    }

    /// <summary>
    /// 다른 슬롯의 속성과 공유 Material을 보존하며 빛과 헬멧 색을 맞춥니다.
    /// </summary>
    private void ApplyState()
    {
        helmetRenderer.GetPropertyBlock(propertyBlock, helmetMaterialIndex);
        propertyBlock.SetColor(baseColorId, isOn ? onColor : offColor);
        helmetRenderer.SetPropertyBlock(propertyBlock, helmetMaterialIndex);
        flashlightLight.enabled = isOn;
    }
}
