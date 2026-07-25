using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AdvancedPS.Core.System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace AdvancedPS.Core.Examples
{
    public class DoTweenShowcase : MonoBehaviour
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

        private void Update()
        {
            // APS reads no input of its own — the game decides what "back" means and steps the stack itself.
            // See documentation §6.2 "Escape close stack".
            if (EscapePressed()) AdvancedPopupSystem.EscapeStep();
        }

        /// <summary>
        /// Escape pressed this frame. Legacy Input Manager only: this sample assembly deliberately doesn't reference
        /// the Input System package (it must compile without it). On the new backend, call EscapeStep() from your own
        /// action instead — the popup side is identical either way.
        /// </summary>
        private static bool EscapePressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            // Fully qualified: the enclosing AdvancedPS.Core.Input namespace shadows the bare name.
            return UnityEngine.Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        private void FixedUpdate()
        {
            _stats.text = $"ActivePopups={AdvancedPopupSystem.ActivePopups.Count}\nActiveOperations={APSStats.ActiveOperationsCount}\nActiveTasks={APSStats.ActiveTasksCount}";
            _stats.text += $"\nActiveTweeners={DOTween.TotalActiveTweeners()}\nActiveSeq={DOTween.TotalActiveSequences()}\nPlaying={DOTween.TotalPlayingTweens()}";
        }

        private void InfintLoop()
        {
            foreach (AdvancedPopup demoPopup in _popups)
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

        private async Task<List<AdvancedPopup>> GeneratePopups()
        {
            List<AdvancedPopup> popups = new List<AdvancedPopup>();
            RectTransform canvasRectTransform = _canvas.GetComponent<RectTransform>();

            float canvasWidth = canvasRectTransform.rect.width;
            float canvasHeight = canvasRectTransform.rect.height;
            int easingTypesCount = Enum.GetValues(typeof(Ease)).Length;

            float popupWidth = Mathf.Clamp(canvasWidth / (CountOfPopups + (CountOfPopups - 1) / 2.0f), 1, 100);
            float spacing = popupWidth / 2;

            for (int i = 0; i < CountOfPopups; i++)
            {
                // Lane B: load a pooled copy from Addressables instead of Instantiate(prefab). Returns null without the
                // Addressables integration or before the index is (re)generated — bail so the demo degrades gracefully.
                AdvancedPopup popup = await AdvancedPopupSystem.SpawnAsync<TweenPopupDemo>(_popupsRoot);
                if (popup == null)
                    break;

                popup.RootTransform.sizeDelta = new Vector2(popupWidth, popupWidth);

                float posX = i * (popupWidth + spacing) - (canvasWidth / 2) + (popupWidth / 2);
                float posY = -canvasHeight / 2 + popup.RootTransform.sizeDelta.y / 2;
                popup.RootTransform.anchoredPosition3D = new Vector3(posX, posY, 0);

                float duration = Random.Range(2f, 8);
                Ease type = (Ease)Random.Range(0, easingTypesCount - 1);

                popup.SetCachedDisplay(
                    DoTweenSettings.Create((rt, seq) => {
                        seq.Append(rt.DOLocalJump(new Vector3(posX, 0, 0), 10f, Random.Range(1, 10), duration)
                           .SetEase(type))
                           .OnStart(() => { rt.sizeDelta = new Vector2(popupWidth, popupWidth); });
                    }),
                    DoTweenSettings.Create((rt, seq) => {
                        seq.Append(rt.DOLocalMove(new Vector3(posX, posY, 0), 1f)
                            .SetEase(Ease.Linear))
                            .OnStart(() => { rt.sizeDelta = new Vector2(popupWidth, popupWidth); });
                    }));

                popups.Add(popup);
            }

            return popups;
        }
    }
}
