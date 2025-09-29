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
    }
}