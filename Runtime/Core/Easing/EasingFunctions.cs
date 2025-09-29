using System;
using UnityEngine;

namespace AdvancedPS.Core.System
{
    public static class EasingFunctions
    {
        // micro-optimizations
        private const float C4 = (2f * Mathf.PI) / 3f; // elastic period for In/Out
        private const float C5 = (2f * Mathf.PI) / 4.5f; // elastic period for InOut
        private const float S = 1.70158f; // back overshoot
        private const float S2 = 2.5949095f; // S * 1.525
        
        private const int Steps = 32; // cache resolution for monotonic curves

        private static readonly EasingType[] Types = (EasingType[])Enum.GetValues(typeof(EasingType));
        private static readonly int TypesCount = Types.Length;
        private static readonly float[,] Cache = new float[TypesCount, Steps + 1];

        private static bool _built;
        
        static EasingFunctions() { BuildCache(); }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrewarmBeforeScene() { BuildCache(); }
        
        private static void BuildCache()
        {
            if (_built) return;
            for (int i = 0; i <= Steps; i++)
            {
                float t = i / (float)Steps;
                for (int typeIndex = 0; typeIndex < TypesCount; typeIndex++)
                {
                    var type = Types[typeIndex];
                    Cache[typeIndex, i] = Calculate(type, t);
                }
            }
            _built = true;
        }

        /// <summary>
        /// Used cached values for Monotonic easings.
        /// </summary>
        public static float Get(EasingType type, float t)
        {
            t = Mathf.Clamp01(t);

            int typeIndex = (int)type;
            float ft = t * Steps;
            int i = (int)ft;
            if (i >= Steps) return Cache[typeIndex, Steps];
            float a = Cache[typeIndex, i];
            float b = Cache[typeIndex, i + 1];
            return Mathf.Lerp(a, b, ft - i);
        }

        /// <summary>
        /// Don't used cached values, calculating for every easing type.
        /// </summary>
        public static float GetExact(EasingType type, float t)
        {
            return Calculate(type, Mathf.Clamp01(t));
        }

        

                private static float Calculate(EasingType type, float t)
        {
            switch (type)
            {
                case EasingType.Linear: return t;

                case EasingType.EaseInSine: return 1f - Mathf.Cos((t * Mathf.PI) / 2f);
                case EasingType.EaseOutSine: return Mathf.Sin((t * Mathf.PI) / 2f);
                case EasingType.EaseInOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;

                case EasingType.EaseInQuad: return t * t;
                case EasingType.EaseOutQuad: { float u = 1f - t; return 1f - u * u; }
                case EasingType.EaseInOutQuad: return t < 0.5f ? 2f * t * t : 1f - ((-2f * t + 2f) * (-2f * t + 2f)) / 2f;

                case EasingType.EaseInCubic: return t * t * t;
                case EasingType.EaseOutCubic: { float u = 1f - t; return 1f - u * u * u; }
                case EasingType.EaseInOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - ((-2f * t + 2f) * (-2f * t + 2f) * (-2f * t + 2f)) / 2f;

                case EasingType.EaseInQuart: return t * t * t * t;
                case EasingType.EaseOutQuart: { float u = 1f - t; return 1f - u * u * u * u; }
                case EasingType.EaseInOutQuart: 
                    if (t < 0.5f) return 8f * t * t * t * t;
                    {
                        float u = -2f * t + 2f;
                        float u2 = u * u;
                        return 1f - (u2 * u2) / 2f;
                    }

                case EasingType.EaseInQuint: return t * t * t * t * t;
                case EasingType.EaseOutQuint: { float u = 1f - t; return 1f - u * u * u * u * u; }
                case EasingType.EaseInOutQuint: 
                    if (t < 0.5f) return 16f * t * t * t * t * t;
                    {
                        float u = -2f * t + 2f;
                        float u2 = u * u;
                        return 1f - (u2 * u2 * u) / 2f;
                    }
                    
                case EasingType.EaseInExpo: return t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
                case EasingType.EaseOutExpo: return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case EasingType.EaseInOutExpo:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return t < 0.5f ? Mathf.Pow(2f, 20f * t - 10f) / 2f : (2f - Mathf.Pow(2f, -20f * t + 10f)) / 2f;

                case EasingType.EaseInCirc: return 1f - Mathf.Sqrt(1f - t * t);
                case EasingType.EaseOutCirc: { float u = t - 1f; return Mathf.Sqrt(1f - u * u); }
                case EasingType.EaseInOutCirc: return t < 0.5f ? (1f - Mathf.Sqrt(1f - (2f * t) * (2f * t))) / 2f : (Mathf.Sqrt(1f - (-2f * t + 2f) * (-2f * t + 2f)) + 1f) / 2f;

                case EasingType.EaseInBack: return (S + 1f) * t * t * t - S * t * t; // 2.70158 = S+1, 1.70158 = S
                case EasingType.EaseOutBack: { float u = t - 1f; return 1f + (S + 1f) * u * u * u + S * u * u; }
                case EasingType.EaseInOutBack:
                    return t < 0.5f
                        ? (Mathf.Pow(2f * t, 2f) * ((S2 + 1f) * 2f * t - S2)) / 2f
                        : (Mathf.Pow(2f * t - 2f, 2f) * ((S2 + 1f) * (2f * t - 2f) + S2) + 2f) / 2f;

                case EasingType.EaseInElastic:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((t * 10f - 10.75f) * C4);
                case EasingType.EaseOutElastic:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * C4) + 1f;
                case EasingType.EaseInOutElastic:
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    return t < 0.5f
                        ? -(Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * C5)) / 2f
                        :  (Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * C5)) / 2f + 1f;

                case EasingType.EaseInBounce:  return 1f - EaseOutBounce(1f - t);
                case EasingType.EaseOutBounce: return EaseOutBounce(t);
                case EasingType.EaseInOutBounce: return t < 0.5f ? (1f - EaseOutBounce(1f - 2f * t)) / 2f : (1f + EaseOutBounce(2f * t - 1f)) / 2f;

                default: return t;
            }
        }

        private static float EaseOutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1)
                return n1 * t * t;
            if (t < 2f / d1)
            {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }
            if (t < 2.5f / d1)
            {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}