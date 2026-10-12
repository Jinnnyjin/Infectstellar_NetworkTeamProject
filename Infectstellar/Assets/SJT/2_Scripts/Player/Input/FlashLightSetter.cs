using System;
using UnityEngine;

public class FlashLightSetter : MonoBehaviour
{
    private Color c_turnOn;
    private Color c_turnOff;
    [SerializeField] private GameObject flashlight; // 손전등 오브젝트
    [SerializeField] private GameObject HelmetMaterial; // 손전등 키고 끔에 따라서 색이 달라질 헬멧 매터리얼

    public Action<bool> D_FlashlightToggle; // 손전등을 키고 켤 때, 외부에 전달할 이벤트 액션
}
