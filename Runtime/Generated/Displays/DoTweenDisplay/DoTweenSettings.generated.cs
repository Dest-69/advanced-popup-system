using System;
using AdvancedPS.Core.System;
using DG.Tweening;
using UnityEngine;

namespace AdvancedPS.Core
{
    [Serializable]
    public class DoTweenSettings : BaseSettings<DoTweenDisplay>
    {
        /// <summary>
        /// Internal factory that produces a fresh Sequence per run.
        /// </summary>
        public Func<RectTransform, Sequence> Factory { get; private set; }
        
        // Engine-level knobs (optional). UnscaledTime is inherited from BaseSettings.
        public bool Recyclable   = true;
        public bool AutoKill     = true;
        public LinkBehaviour Link = LinkBehaviour.PauseOnDisable | LinkBehaviour.KillOnDestroy;

        /// <summary>
        /// Static helper to keep call-sites terse.
        /// </summary>
        public static DoTweenSettings Create(Action<RectTransform, Sequence> build)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));

            DoTweenSettings settings = new DoTweenSettings();
            
            settings.Factory = (rect) =>
            {
                Sequence seq = DOTween.Sequence();
                build(rect, seq);
                return seq;
            };
            
            return settings;
        }
        
        /// <summary>
        /// Fluent helpers (optional).
        /// </summary>
        public DoTweenSettings WithUnscaledTime(bool value) { UnscaledTime = value; return this; }
        public DoTweenSettings WithRecyclable(bool value)   { Recyclable   = value; return this; }
        public DoTweenSettings WithAutoKill(bool value)     { AutoKill     = value; return this; }
        public DoTweenSettings WithLink(LinkBehaviour lb)   { Link         = lb;    return this; }
    }
}
