using System;
using UnityEngine;

namespace AdvancedPS.Core.System
{
    public static class EasingFunctions
    {
        private const int Steps = 20;
        private static readonly float[,] CachedValues;

        static EasingFunctions()
        {
            CachedValues = new float[Enum.GetValues(typeof(EasingType)).Length, Steps + 1];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            for (int i = 0; i <= Steps; i++)
            {
                float t = i / (float)Steps;
                foreach (EasingType type in Enum.GetValues(typeof(EasingType)))
                {
                    float value = CalculateEasingValue(type, t);
                    CachedValues[(int)type, i] = value;
                }
            }
        }

        public static float GetEasingValue(EasingType type, float t)
        {
            int index = (int)(t * Steps);
            if (index >= Steps)
            {
                return CachedValues[(int)type, Steps];
            }

            float startValue = CachedValues[(int)type, index];
            float endValue = CachedValues[(int)type, index + 1];
            float lerpT = t * Steps - index;

            return Mathf.Lerp(startValue, endValue, lerpT);
        }

        private static float CalculateEasingValue(EasingType type, float t)
        {
            switch (type)
            {
                case EasingType.Linear: return t;
                case EasingType.EaseInSine: return 1 - Mathf.Cos((t * Mathf.PI) / 2);
                case EasingType.EaseOutSine: return Mathf.Sin((t * Mathf.PI) / 2);
                case EasingType.EaseInOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1) / 2;
                case EasingType.EaseInQuad: return t * t;
                case EasingType.EaseOutQuad: return 1 - (1 - t) * (1 - t);
                case EasingType.EaseInOutQuad: return t < 0.5 ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
                case EasingType.EaseInCubic: return t * t * t;
                case EasingType.EaseOutCubic: return 1 - Mathf.Pow(1 - t, 3);
                case EasingType.EaseInOutCubic: return t < 0.5 ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
                case EasingType.EaseInQuart: return t * t * t * t;
                case EasingType.EaseOutQuart: return 1 - Mathf.Pow(1 - t, 4);
                case EasingType.EaseInOutQuart: return t < 0.5 ? 8 * t * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 4) / 2;
                case EasingType.EaseInQuint: return t * t * t * t * t;
                case EasingType.EaseOutQuint: return 1 - Mathf.Pow(1 - t, 5);
                case EasingType.EaseInOutQuint: return t < 0.5 ? 16 * t * t * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 5) / 2;
                case EasingType.EaseInExpo: return t == 0 ? 0 : Mathf.Pow(2, 10 * t - 10);
                case EasingType.EaseOutExpo: return t == 1 ? 1 : 1 - Mathf.Pow(2, -10 * t);
                case EasingType.EaseInOutExpo: return t == 0 ? 0 : t == 1 ? 1 : t < 0.5 ? Mathf.Pow(2, 20 * t - 10) / 2 : (2 - Mathf.Pow(2, -20 * t + 10)) / 2;
                case EasingType.EaseInCirc: return 1 - Mathf.Sqrt(1 - Mathf.Pow(t, 2));
                case EasingType.EaseOutCirc: return Mathf.Sqrt(1 - Mathf.Pow(t - 1, 2));
                case EasingType.EaseInOutCirc: return t < 0.5 ? (1 - Mathf.Sqrt(1 - Mathf.Pow(2 * t, 2))) / 2 : (Mathf.Sqrt(1 - Mathf.Pow(-2 * t + 2, 2)) + 1) / 2;
                case EasingType.EaseInBack: return 2.70158f * t * t * t - 1.70158f * t * t;
                case EasingType.EaseOutBack: return 1 + 2.70158f * Mathf.Pow(t - 1, 3) + 1.70158f * Mathf.Pow(t - 1, 2);
                case EasingType.EaseInOutBack: return t < 0.5 ? (Mathf.Pow(2 * t, 2) * ((2.5949095f + 1) * 2 * t - 2.5949095f)) / 2 : (Mathf.Pow(2 * t - 2, 2) * ((2.5949095f + 1) * (t * 2 - 2) + 2.5949095f) + 2) / 2;
                case EasingType.EaseInElastic: return t == 0 ? 0 : t == 1 ? 1 : -Mathf.Pow(2, 10 * t - 10) * Mathf.Sin((t * 10 - 10.75f) * (2 * Mathf.PI) / 3);
                case EasingType.EaseOutElastic: return t == 0 ? 0 : t == 1 ? 1 : Mathf.Pow(2, -10 * t) * Mathf.Sin((t * 10 - 0.75f) * (2 * Mathf.PI) / 3) + 1;
                case EasingType.EaseInOutElastic: return t == 0 ? 0 : t == 1 ? 1 : t < 0.5 ? -(Mathf.Pow(2, 20 * t - 10) * Mathf.Sin((20 * t - 11.125f) * (2 * Mathf.PI) / 4.5f)) / 2 : (Mathf.Pow(2, -20 * t + 10) * Mathf.Sin((20 * t - 11.125f) * (2 * Mathf.PI) / 4.5f)) / 2 + 1;
                case EasingType.EaseInBounce: return 1 - EaseOutBounce(1 - t);
                case EasingType.EaseOutBounce: return EaseOutBounce(t);
                case EasingType.EaseInOutBounce: return t < 0.5 ? (1 - EaseOutBounce(1 - 2 * t)) / 2 : (1 + EaseOutBounce(2 * t - 1)) / 2;
                default: return t;
            }
        }

        private static float EaseOutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1 / d1) return n1 * t * t;
            if (t < 2 / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
            if (t < 2.5 / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
            return n1 * (t -= 2.625f / d1) * t + 0.984375f;
        }
    }
}