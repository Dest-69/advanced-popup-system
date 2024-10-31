using System;
using System.Collections.Generic;
using AdvancedPS.Core.System;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace AdvancedPS.Core.Examples
{
    public class PerformanceShowcase : MonoBehaviour
    {
        [SerializeField] private int CountOfPopups;
        
        [SerializeField] private DemoPopup _demoPopupPrefab;
        
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Transform _popupsRoot;
        
        [SerializeField] private Button _buttonStart;
        [SerializeField] private Button _buttonStop;
        [SerializeField] private Text _infoPanel;
        
        private List<DemoPopup> _popups;
        private bool isStop;

        private void Awake()
        {
            _buttonStart.onClick.AddListener(() =>
            {
                isStop = false;
                InfintLoop();
                _buttonStart.interactable = false;
                _buttonStop.interactable = true;
            });
            
            _buttonStop.interactable = false;
            _buttonStop.onClick.AddListener(() =>
            {
                isStop = true;
                _buttonStart.interactable = true;
                _buttonStop.interactable = false;
            });
        }
        
        private void Start()
        {
            _popups = GeneratePopups();
            _infoPanel.text += $"\n Popup's count: {_popups.Count}";
        }
        
        private void InfintLoop()
        {
            foreach (DemoPopup demoPopup in _popups)
            {
                void tempExec()
                {
                    if (isStop) return;
                    demoPopup.Show().OnComplete(() =>
                    {
                        demoPopup.Hide().OnComplete(tempExec);
                    });
                }

                tempExec();
            }
        }
        
        private List<DemoPopup> GeneratePopups()
        {
            List<DemoPopup> popups = new List<DemoPopup>();
            RectTransform canvasRectTransform = _canvas.GetComponent<RectTransform>();
            
            float canvasWidth = canvasRectTransform.rect.width;
            float canvasHeight = canvasRectTransform.rect.height;
            int easingTypesCount = Enum.GetValues(typeof(EasingType)).Length;

            float popupWidth = Mathf.Clamp(canvasWidth / (CountOfPopups + (CountOfPopups - 1) / 2.0f), 1, 100);
            float spacing = popupWidth / 2;

            for (int i = 0; i < CountOfPopups; i++)
            {
                DemoPopup popup = Instantiate(_demoPopupPrefab, _popupsRoot);
                popup.RootTransform.sizeDelta = new Vector2(popupWidth, popupWidth);
                
                float posX = i * (popupWidth + spacing) - (canvasWidth / 2) + (popupWidth / 2);
                float posY = -canvasHeight / 2 + popup.RootTransform.sizeDelta.y / 2;
                popup.RootTransform.anchoredPosition3D = new Vector3(posX, posY, 0);

                float duration = Random.Range(0.5f, 4);
                EasingType type = (EasingType)Random.Range(0, easingTypesCount - 1);
                
                popup.Init();
                popup.SetCachedDisplay<SlideDisplay, SlideDisplay>(new SlideSettings
                    {
                        Duration = duration,
                        Easing = type,
                        TargetRectPosition = new Vector3(posX, 0, 0),
                        TargetRectSize = new Vector2(popupWidth, popupWidth)
                    },
                    new SlideSettings
                    {
                        Duration = duration,
                        Easing = type,
                        TargetRectPosition = new Vector3(posX, posY, 0),
                        TargetRectSize = new Vector2(popupWidth, popupWidth)
                    });
                popups.Add(popup);
            }
            
            return popups;
        }
    }
}
