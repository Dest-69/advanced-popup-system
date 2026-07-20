using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AdvancedPS.Core.Examples
{
    /// <summary>
    /// Tiny uGUI builders shared by the Addressables showcase — just enough to make the demo visible from code, so the
    /// sample needs no hand-authored UI. Not a UI toolkit; keep it minimal.
    /// </summary>
    internal static class ShowcaseUI
    {
        private static Font _font;

        /// <summary> A built-in font, resilient across editor versions (LegacyRuntime.ttf, or the older Arial.ttf). </summary>
        public static Font Font()
        {
            if (_font != null) return _font;
            foreach (string n in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try { _font = Resources.GetBuiltinResource<Font>(n); } catch { /* try the next name */ }
                if (_font != null) break;
            }
            return _font;
        }

        /// <summary> Adds a stretched, non-interactive Text label filling <paramref name="parent"/>. </summary>
        public static Text Label(Transform parent, string text, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Font();
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }

        /// <summary> Adds a labeled Button (fixed height) under <paramref name="parent"/> and wires its click. </summary>
        public static Button Button(Transform parent, string label, UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(220f, 34f);

            go.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.17f, 0.95f);
            Button btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);

            Label(rt, label, 15, TextAnchor.MiddleCenter, Color.white);
            return btn;
        }
    }
}
