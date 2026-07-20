using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AdvancedPS.Core.System
{
    /// <summary>
    /// Extension seam that materializes popups from an external source (e.g. Addressables) on demand.
    /// Core holds a single optional <see cref="AdvancedPopupSystem.Resolver"/>; while it is null the system is
    /// scene-only — popups must already exist in a loaded scene, exactly as before this seam existed.
    /// The optional Addressables integration assembly (compiled under the <c>APS_ADDRESSABLES</c> define) registers
    /// an implementation at load via <c>RuntimeInitializeOnLoadMethod</c>.
    /// </summary>
    /// <remarks>
    /// Responsibility split is deliberate: <b>core</b> owns the address catalog (the generated
    /// <c>AddressablePopupIndex</c>), the dedup ("is this popup already live?") and the scene-wins rule; a resolver
    /// only turns an address into a live instance and releases it again. That keeps the runtime dependency-light —
    /// nothing here references Addressables.
    /// </remarks>
    public interface IPopupResolver
    {
        /// <summary>
        /// Load and instantiate the popup asset registered at <paramref name="address"/> under
        /// <paramref name="parent"/>. The new instance's <c>Init()</c> self-registers it with the system, so callers
        /// find it in the usual registries afterwards. Returns null on failure (the resolver logs the reason).
        /// </summary>
        /// <param name="address">Addressable key from the generated index.</param>
        /// <param name="parent">Where to parent the instance — typically <see cref="AdvancedPopupSystem.Root"/>.</param>
        /// <param name="token">Cancels the load in flight (e.g. a following transition on the same popup).</param>
        Task<IAdvancedPopup> LoadAsync(string address, Transform parent, CancellationToken token);

        /// <summary>
        /// Release an instance previously produced by <see cref="LoadAsync"/> and its underlying handle so the assets
        /// can unload. A no-op for instances this resolver did not create (e.g. scene-authored popups).
        /// </summary>
        void Release(IAdvancedPopup popup);
    }
}
