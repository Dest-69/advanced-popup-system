using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using UnityEngine;
using UnityEngine.UI;

namespace AdvancedPS.Core.Examples
{
    public class EasingShowcase : MonoBehaviour
    {
        [SerializeField] private InfoBlock _infoBlockPrefab;

        [SerializeField] private Canvas _canvas;
        [SerializeField] private Transform _popupsRoot;
        [SerializeField] private Transform _infoRoot;

        [SerializeField] private Button _buttonShow;
        [SerializeField] private Button _buttonHide;

        private List<AdvancedPopup> _popups;

        private void Awake()
        {
            _buttonShow.onClick.AddListener(() =>
            {
                // Lane B (SpawnAsync) popups aren't part of layer batches, so show the spawned grid manually
                // instead of LayerShow(MENU).
                foreach (AdvancedPopup popup in _popups)
                    popup.Show();
                _buttonShow.interactable = false;
                _buttonHide.interactable = true;
            });

            // Disabled until the Addressable popups finish spawning (GeneratePopups is async now).
            _buttonShow.interactable = false;
            _buttonHide.interactable = false;
            _buttonHide.onClick.AddListener(() =>
            {
                foreach (AdvancedPopup popup in _popups)
                    popup.Hide();
                _buttonShow.interactable = true;
                _buttonHide.interactable = false;
            });
        }

        private async void Start()
        {
            _popups = await GeneratePopups();
            _buttonShow.interactable = true;
        }

        private async Task<List<AdvancedPopup>> GeneratePopups()
        {
            List<AdvancedPopup> popups = new List<AdvancedPopup>();
            RectTransform canvasRectTransform = _canvas.GetComponent<RectTransform>();

            float canvasWidth = canvasRectTransform.rect.width;
            float canvasHeight = canvasRectTransform.rect.height;
            int easingTypesCount = Enum.GetValues(typeof(EasingType)).Length;

            float popupWidth = canvasWidth / (easingTypesCount + (easingTypesCount - 1) / 2.0f);
            float spacing = popupWidth / 2;

            float initialY = -250;
            float step = 25;
            int direction = 1;

            for (int i = 0; i < easingTypesCount; i++)
            {
                // Lane B: one pooled copy per easing type from Addressables instead of Instantiate(prefab). Null without
                // the Addressables integration or before the index is (re)generated — bail so the demo degrades gracefully.
                AdvancedPopup popup = await AdvancedPopupSystem.SpawnAsync<EasingPopupDemo>(_popupsRoot);
                if (popup == null)
                    break;

                popup.RootTransform.sizeDelta = new Vector2(popupWidth, popup.RootTransform.sizeDelta.y);

                float posX = i * (popupWidth + spacing) - (canvasWidth / 2) + (popupWidth / 2);
                float posY = -canvasHeight / 2 - popup.RootTransform.sizeDelta.y / 2;
                popup.RootTransform.anchoredPosition = new Vector2(posX, posY);

                popup.SetCachedDisplay(
                    new SlideSettings
                    {
                        Duration = 1f,
                        Easing = (EasingType)i,
                        TargetRectPosition = new Vector3(posX, 0, 0),
                        TargetRectSize = new Vector2(popupWidth, popupWidth)
                    },
                    new SlideSettings
                    {
                        Duration = 1f,
                        Easing = (EasingType)i,
                        TargetRectPosition = new Vector3(posX, posY, 0),
                        TargetRectSize = new Vector2(popupWidth, popupWidth)
                    }
                );
                popups.Add(popup);

                InfoBlock infoBlock = Instantiate(_infoBlockPrefab, _infoRoot);
                RectTransform infoRect = infoBlock.GetComponent<RectTransform>();
                infoRect.anchoredPosition = new Vector2(posX, initialY);

                infoBlock.SetText(Enum.GetName(typeof(EasingType), (EasingType)i));

                if (i % 3 == 0 && i != 0)
                {
                    direction *= -1;
                }
                initialY += direction * step;
            }

            return popups;
        }
    }
}
