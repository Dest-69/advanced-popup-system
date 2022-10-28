using UnityEngine;
using UnityEngine.UI;

public class AdvancedPopup_ToolBarView_Demo : MonoBehaviour
{
    [SerializeField] private Button _shopBut;
    [SerializeField] private Button _inventoryBut;
    
    private void Start()
    {
        _shopBut.onClick.AddListener(() => 
            AdvancedPopupSystem.ForceShow<SmoothScaleDisplay, SmoothScaleDisplay>("Shop"));
        
        _inventoryBut.onClick.AddListener(() => 
            AdvancedPopupSystem.ForceShow<SmoothScaleDisplay, SmoothScaleDisplay>("Inventory"));
    }
}
