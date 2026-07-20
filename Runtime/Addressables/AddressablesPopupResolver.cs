using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AdvancedPS.Core.System
{
    /// <summary>
    /// Addressables-backed <see cref="IPopupResolver"/> — the optional integration that lets popups load from
    /// Addressables on demand. This whole assembly compiles only under the <c>APS_ADDRESSABLES</c> define, which the
    /// asmdef auto-activates when <c>com.unity.addressables</c> is installed; core never references Addressables, so
    /// the runtime stays dependency-light. Registers itself into <see cref="AdvancedPopupSystem.Resolver"/> at load.
    /// </summary>
    public sealed class AddressablesPopupResolver : IPopupResolver
    {
        /// <summary>
        /// Instantiate handle per popup so <see cref="Release"/> can hand it back to Addressables — ReleaseInstance
        /// both destroys the GameObject and drops the ref-count so the assets can unload.
        /// </summary>
        private readonly Dictionary<IAdvancedPopup, AsyncOperationHandle<GameObject>> _handles = new();

        /// <summary>
        /// Installs the resolver before the first scene loads (and before AdvancedPopupSystem's AfterSceneLoad preload
        /// pass), so it is present by the time any popup is shown. Does not overwrite a resolver the user set.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            AdvancedPopupSystem.Resolver ??= new AddressablesPopupResolver();
        }

        /// <inheritdoc/>
        public async Task<IAdvancedPopup> LoadAsync(string address, Transform parent, CancellationToken token)
        {
            AsyncOperationHandle<GameObject> handle = default;
            try
            {
                handle = Addressables.InstantiateAsync(address, parent, false);
                await handle.Task;
            }
            catch (Exception ex)
            {
                APLogger.LogError($"<color=red>[AddressablesPopupResolver]</color> Exception loading popup at '{address}': {ex.Message}");
                if (handle.IsValid()) Addressables.ReleaseInstance(handle);
                return null;
            }

            // A transition that superseded this load cancelled us mid-flight — release what we made and bail.
            if (token.IsCancellationRequested)
            {
                if (handle.IsValid()) Addressables.ReleaseInstance(handle);
                return null;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                APLogger.LogError($"<color=red>[AddressablesPopupResolver]</color> Failed to load popup at '{address}'.");
                if (handle.IsValid()) Addressables.ReleaseInstance(handle);
                return null;
            }

            if (!handle.Result.TryGetComponent(out IAdvancedPopup popup))
            {
                APLogger.LogError($"<color=red>[AddressablesPopupResolver]</color> Asset at '{address}' has no IAdvancedPopup component.");
                Addressables.ReleaseInstance(handle);
                return null;
            }

            _handles[popup] = handle;
            return popup;
        }

        /// <inheritdoc/>
        public void Release(IAdvancedPopup popup)
        {
            if (popup == null) return;

            if (_handles.TryGetValue(popup, out AsyncOperationHandle<GameObject> handle))
            {
                _handles.Remove(popup);
                if (handle.IsValid())
                    Addressables.ReleaseInstance(handle); // destroys the GameObject + drops the ref-count
            }
            else
            {
                // Not one of ours (e.g. a scene-authored popup) — just destroy the GameObject.
                UnityEngine.Object.Destroy(popup.gameObject);
            }
        }
    }
}
