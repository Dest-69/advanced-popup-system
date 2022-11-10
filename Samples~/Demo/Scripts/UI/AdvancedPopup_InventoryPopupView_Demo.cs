using UnityEngine;
using UnityEngine.UI;

public class AdvancedPopup_InventoryPopupView_Demo : AdvancedPopup
{
    [SerializeField] private Button CloseBut;
    public override void Init()
    {
        base.Init();
        PopupName = "Inventory";
        PopupLayer = PopupLayerEnum.FIFTH_NAME | PopupLayerEnum.FIRST_NAME;
    }

    private void Start()
    {
        // Close this popup with all incerted popups (deepPopup)
        CloseBut.onClick.AddListener(() => Hide<SmoothScaleDisplay>(true));
    }
}
