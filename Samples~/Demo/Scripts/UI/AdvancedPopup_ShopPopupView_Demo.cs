using UnityEngine;
using UnityEngine.UI;

public class AdvancedPopup_ShopPopupView_Demo : AdvancedPopup
{
    [SerializeField] private Button CloseBut;
    public override void Init()
    {
        base.Init();
        PopupName = "Shop";
    }

    private void Start()
    {
        // Close this popup with all incerted popups (deepPopup)
        CloseBut.onClick.AddListener(() => Hide<SmoothScaleDisplay>(true));
    }
}