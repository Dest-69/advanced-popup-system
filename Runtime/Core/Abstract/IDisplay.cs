using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AdvancedPS.Core.System
{
    public interface IDisplay
    {
        void ShowInstantlyMethod(RectTransform transform, IDisplaySettings settings);
        void HideInstantlyMethod(RectTransform transform, IDisplaySettings settings);
        Task ShowMethod(RectTransform transform, IDisplaySettings settings, CancellationToken cancellationToken);
        Task HideMethod(RectTransform transform, IDisplaySettings settings, CancellationToken cancellationToken);
    }
    
    public interface IDisplay<in TSettings> : IDisplay where TSettings : IDisplaySettings
    {
        void ShowInstantlyMethod(RectTransform transform, TSettings settings);
        void HideInstantlyMethod(RectTransform transform, TSettings settings);
        Task ShowMethod(RectTransform transform, TSettings settings, CancellationToken cancellationToken);
        Task HideMethod(RectTransform transform, TSettings settings, CancellationToken cancellationToken);
    }
    
    public abstract class DisplayBase<TSettings> : IDisplay<TSettings> where TSettings : IDisplaySettings, new()
    {
        void IDisplay.ShowInstantlyMethod(RectTransform transform, IDisplaySettings settings) =>
            ShowInstantlyMethod(transform, (TSettings)settings);
        void IDisplay.HideInstantlyMethod(RectTransform transform, IDisplaySettings settings) =>
            HideInstantlyMethod(transform, (TSettings)settings);
        Task IDisplay.ShowMethod(RectTransform transform, IDisplaySettings settings, CancellationToken cancellationToken) =>
            ShowMethod(transform, (TSettings)settings, cancellationToken);
        Task IDisplay.HideMethod(RectTransform transform, IDisplaySettings settings, CancellationToken cancellationToken) =>
            HideMethod(transform, (TSettings)settings, cancellationToken);
        
        /// <summary>
        /// Logic for instant popup show.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <returns></returns>
        public abstract void ShowInstantlyMethod(RectTransform transform, TSettings settings);

        /// <summary>
        /// Logic for instant popup hide.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <returns></returns>
        public abstract void HideInstantlyMethod(RectTransform transform, TSettings settings);
        
        /// <summary>
        /// Logic for popup showing animation.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public abstract Task ShowMethod(RectTransform transform, TSettings settings, CancellationToken cancellationToken);

        /// <summary>
        /// Logic for popup hiding animation.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public abstract Task HideMethod(RectTransform transform, TSettings settings, CancellationToken cancellationToken);
    }
}
