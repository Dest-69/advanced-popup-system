using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;

namespace AdvancedPS.Core
{
    /// <summary>
    /// A popup that opens with typed per-open data. Declare the data the popup needs once
    /// (<c>class RewardPopup : AdvancedPopup&lt;RewardData&gt;</c>) and implement <see cref="Bind"/> — the single
    /// place where data meets the UI. Opened through <see cref="Show(TData, IDisplaySettings)"/> or
    /// <see cref="AdvancedPopupSystem.Show{TPopup, TData}(TData, IDisplaySettings)"/>, the data is bound
    /// <b>before</b> the popup becomes visible, so it can never flash unconfigured content.
    /// The last bound data is kept (<see cref="Data"/> / <see cref="HasData"/>): data-less re-shows (hotkeys, the
    /// escape stack, <c>LayerShow</c>, <c>SwitchShowHide</c>) simply reopen the popup with what it showed last and
    /// never re-run <see cref="Bind"/>. Spawned copies are the exception — <c>Despawn</c> clears their data so a
    /// pooled instance can't carry the previous content into its next use.
    /// </summary>
    public abstract class AdvancedPopup<TData> : AdvancedPopup, IDataPopup
    {
        /// <summary> The data bound last; default until <see cref="HasData"/> is true. </summary>
        public TData Data { get; private set; }

        /// <summary> True while the popup holds bound data (set by <see cref="SetData"/>, reset by <see cref="ClearData"/>). </summary>
        public bool HasData { get; private set; }

        /// <summary>
        /// Bind <paramref name="data"/> to the UI — the single place data meets this popup. Runs whenever new data
        /// arrives (<see cref="SetData"/> or any Show overload taking data) and never on data-less re-shows.
        /// </summary>
        protected abstract void Bind(TData data);

        /// <summary>
        /// Decide whether <paramref name="next"/> counts as the same data as <paramref name="current"/> — when true,
        /// <see cref="SetData"/> skips the re-<see cref="Bind"/>. The default is <c>false</c> (always re-bind):
        /// skipping a bind on data that actually changed is a stale-UI bug, while a redundant bind is only wasted
        /// work. Override it when this popup's bind is expensive and callers may repeat identical data — e.g.
        /// <c>current.Equals(next)</c> for record/value data, or compare a version field.
        /// </summary>
        protected virtual bool IsSameData(TData current, TData next) => false;

        /// <summary>
        /// Bind new data now, without showing. On a visible popup the UI simply updates live. Skipped entirely when
        /// <see cref="IsSameData"/> reports the data didn't change.
        /// </summary>
        public void SetData(TData data)
        {
            if (HasData && IsSameData(Data, data)) return;

            Data = data;
            HasData = true;
            Bind(data);
        }

        /// <summary>
        /// Forget the held data (the already-built UI is not touched). Called automatically by
        /// <c>AdvancedPopupSystem.Despawn</c> on spawned copies.
        /// </summary>
        public void ClearData()
        {
            Data = default;
            HasData = false;
        }

        /// <summary>
        /// Bind <paramref name="data"/>, then show — the data lands before the popup is visible. On an
        /// already-visible popup this just updates the content live.
        /// </summary>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public Operation Show(TData data, IDisplaySettings settings = null)
        {
            return new Operation(async token =>
            {
                SetData(data);
                await ShowAsync(token, settings);
            });
        }

        /// <summary>
        /// Awaitable <see cref="Show(TData, IDisplaySettings)"/>: bind <paramref name="data"/>, then run the show.
        /// </summary>
        /// <param name="token"> (Optional) For control Task life-cycle. </param>
        /// <param name="settings"> The settings for the animation. If not provided, the default settings will be used. </param>
        public async Task ShowAsync(TData data, CancellationToken token = default, IDisplaySettings settings = null)
        {
            SetData(data);
            await ShowAsync(token, settings);
        }
    }
}
