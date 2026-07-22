using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace AdvancedPS.Core.Examples
{
    public class PerformanceShowcase : MonoBehaviour
    {
        [SerializeField] private int CountOfPopups;

        [SerializeField] private Canvas _canvas;
        [SerializeField] private Transform _popupsRoot;

        [SerializeField] private Button _buttonStart;
        [SerializeField] private Button _buttonStop;
        [SerializeField] private Text _infoPanel;
        [SerializeField] private Text _stats;

        private List<AdvancedPopup> _popups;
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

            // Disabled until the Addressable popups finish spawning (GeneratePopups is async now).
            _buttonStart.interactable = false;
            _buttonStop.interactable = false;
            _buttonStop.onClick.AddListener(() =>
            {
                isStop = true;
                _buttonStart.interactable = true;
                _buttonStop.interactable = false;
            });
        }

        private async void Start()
        {
            _popups = await GeneratePopups();
            _infoPanel.text += $"\n Max popup's count: {_popups.Count}";
            _buttonStart.interactable = true;
        }

        private void FixedUpdate()
        {
            _stats.text = $"ActivePopups={AdvancedPopupSystem.ActivePopups.Count}\nActiveOperations={APSStats.ActiveOperationsCount}\nActiveTasks={APSStats.ActiveTasksCount}";
        }

        private void InfintLoop()
        {
            foreach (AdvancedPopup demoPopup in _popups)
            {
                void tempExec()
                {
                    if (isStop || !Application.isPlaying) return;

                    demoPopup.Show().OnComplete(() =>
                    {
                        demoPopup.Hide().OnComplete(tempExec);
                    });
                }

                tempExec();
            }
        }

        private async Task<List<AdvancedPopup>> GeneratePopups()
        {
            List<AdvancedPopup> popups = new List<AdvancedPopup>();
            RectTransform canvasRectTransform = _canvas.GetComponent<RectTransform>();

            float canvasWidth = canvasRectTransform.rect.width;
            float canvasHeight = canvasRectTransform.rect.height;
            int easingTypesCount = Enum.GetValues(typeof(EasingType)).Length;

            float popupWidth = Mathf.Clamp(canvasWidth / (CountOfPopups + (CountOfPopups - 1) / 2.0f), 1, 100);
            float spacing = popupWidth / 2;

            for (int i = 0; i < CountOfPopups; i++)
            {
                // Lane B: load a pooled copy from Addressables instead of Instantiate(prefab). Returns null without the
                // Addressables integration or before the index is (re)generated — bail so the demo degrades gracefully.
                AdvancedPopup popup = await AdvancedPopupSystem.SpawnAsync<PerformancePopupDemo>(_popupsRoot);
                if (popup == null)
                    break;

                popup.RootTransform.sizeDelta = new Vector2(popupWidth, popupWidth);

                float posX = i * (popupWidth + spacing) - (canvasWidth / 2) + (popupWidth / 2);
                float posY = -canvasHeight / 2 + popup.RootTransform.sizeDelta.y / 2;
                popup.RootTransform.anchoredPosition3D = new Vector3(posX, posY, 0);

                float duration = Random.Range(0.5f, 4);
                EasingType type = (EasingType)Random.Range(0, easingTypesCount - 1);

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
