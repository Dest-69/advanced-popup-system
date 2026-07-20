using System.Threading;
using UnityEngine;

namespace AdvancedPS.Core.Utils
{
    public static class TaskUtils
    {
        /// <summary>
        /// Checks if operation already cancelled.
        /// </summary>
        public static bool OperationCancelled(CancellationToken cancellationToken) => cancellationToken.IsCancellationRequested || !Application.isPlaying;
        public static CancellationTokenSource UpdateCancellationTokenSource(CancellationTokenSource source = null, bool dispose = true, CancellationToken linkedToken = default)
        {
            if (source != null)
            {
                source.Cancel();
                if (dispose) source.Dispose();
            }
            
            return linkedToken == default
                ? new CancellationTokenSource()
                : CancellationTokenSource.CreateLinkedTokenSource(linkedToken);
        }

        /// <summary>
        /// Terminal teardown for a source (e.g. on <c>OnDestroy</c>): cancels any in-flight transition and disposes it,
        /// without creating a replacement. Null-safe. Use this instead of a hand-rolled Cancel()/Dispose() pair.
        /// </summary>
        public static void CancelAndDispose(CancellationTokenSource source)
        {
            if (source == null) return;

            source.Cancel();
            source.Dispose();
        }
    }
}