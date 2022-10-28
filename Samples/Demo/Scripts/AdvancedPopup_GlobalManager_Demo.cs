using UnityEngine;

public class AdvancedPopup_GlobalManager_Demo : MonoBehaviour
{
    private void Start()
    {
        AdvancedPopupSystem.ForceShow<SmoothScaleDisplay, ImmediatelyScaleDisplay>("Shop");
    }
}
