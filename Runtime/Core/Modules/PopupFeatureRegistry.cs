using System.Collections.Generic;
using AdvancedPS.Core.System;

namespace AdvancedPS.Core
{
    /// <summary>
    /// Maps interaction feature flags to stateless handlers, in priority order (first that grabs wins). Built-ins are
    /// registered once (resize before drag — a grip near an edge should win over the drag zone beneath it). Call
    /// <see cref="Register"/> to add or override a handler: this is the open extension point, mirroring
    /// <see cref="DisplayRegistry"/> for displays. Holds only stateless handlers (no per-scene state → no leak guards).
    /// </summary>
    public static class PopupFeatureRegistry
    {
        public readonly struct Entry
        {
            public readonly PopupFeatureEnum Flag;
            public readonly IPopupFeatureHandler Handler;

            public Entry(PopupFeatureEnum flag, IPopupFeatureHandler handler)
            {
                Flag = flag;
                Handler = handler;
            }
        }

        private static readonly List<Entry> _entries = new List<Entry>();

        static PopupFeatureRegistry()
        {
            Register(PopupFeatureEnum.Resizable, new ResizeFeatureHandler());
            Register(PopupFeatureEnum.Draggable, new DragFeatureHandler());
        }

        /// <summary> Registered handlers, highest priority first. </summary>
        public static IReadOnlyList<Entry> Entries => _entries;

        /// <summary>
        /// Add or give priority to a feature handler. <paramref name="prepend"/> = insert before the built-ins so it is
        /// tried first (e.g. to override the default drag/resize behaviour).
        /// </summary>
        public static void Register(PopupFeatureEnum flag, IPopupFeatureHandler handler, bool prepend = false)
        {
            if (handler == null) return;
            Entry entry = new Entry(flag, handler);
            if (prepend) _entries.Insert(0, entry);
            else _entries.Add(entry);
        }
    }
}
