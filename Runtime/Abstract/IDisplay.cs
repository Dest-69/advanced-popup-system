using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AdvancedPS.Core.System
{
    public interface IDisplay
    {
        /// <summary>
        /// Logic for popup showing animation.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task ShowMethod(RectTransform transform, BaseSettings settings, CancellationToken cancellationToken);

        /// <summary>
        /// Logic for popup hiding animation.
        /// </summary>
        /// <param name="transform"> RectTransform of root popup GameObject. </param>
        /// <param name="settings"> The settings for the animation. If null, the default settings will be used. </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task HideMethod(RectTransform transform, BaseSettings settings, CancellationToken cancellationToken);
    }
}
