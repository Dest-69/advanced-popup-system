using System;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.Utils;

namespace AdvancedPS.Core.System
{
    /// <summary>
    /// How an <see cref="Operation"/> finished — <see cref="Running"/> while the wrapped work is still in flight.
    /// </summary>
    public enum OperationStatus
    {
        /// <summary> The operation is still executing. </summary>
        Running,
        /// <summary> The operation finished without being cancelled and without an exception. </summary>
        Succeeded,
        /// <summary>
        /// The operation was cancelled — via <see cref="Operation.Cancel"/>, by a newer show/hide superseding it on the
        /// same popup, or by exiting play mode.
        /// </summary>
        Cancelled,
        /// <summary>
        /// An exception escaped the operation. It is logged through <see cref="APLogger"/> and kept in
        /// <see cref="Operation.Error"/>.
        /// </summary>
        Faulted
    }

    /// <summary>
    /// A lightweight, self-starting wrapper around one async popup operation. The wrapped delegate starts immediately
    /// on construction under the operation's own <see cref="CancellationToken"/>; exceptions are caught and logged
    /// (never thrown at the caller), and the outcome is observable via <see cref="Status"/> / <see cref="Error"/> or
    /// the <see cref="OnComplete(Action{Operation})"/> callback. Wrap custom async popup flows in an Operation instead
    /// of a bare fire-and-forget task so they get the same guarantees.
    /// </summary>
    public class Operation
    {
        /// <summary>
        /// Where the operation currently stands. Any value other than <see cref="OperationStatus.Running"/> is final.
        /// </summary>
        public OperationStatus Status { get; private set; }

        /// <summary>
        /// The exception that failed the operation; null unless <see cref="Status"/> is
        /// <see cref="OperationStatus.Faulted"/>.
        /// </summary>
        public Exception Error { get; private set; }

        /// <summary> True once the operation reached its final status. </summary>
        public bool IsCompleted => Status != OperationStatus.Running;

        private readonly CancellationTokenSource _source;
        private Action _onComplete;
        private Action<Operation> _onFinished;

        /// <summary>
        /// Starts <paramref name="operation"/> immediately. The <see cref="CancellationToken"/> it receives is this
        /// operation's own token — forward it into every awaited call inside (e.g. <c>GetPopupAsync</c> /
        /// <c>ShowAsync</c>) so <see cref="Cancel"/> reaches them.
        /// </summary>
        public Operation(Func<CancellationToken, Task> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            _source = TaskUtils.UpdateCancellationTokenSource(dispose: false);
            _ = ExecuteAsync(operation);
        }

        /// <summary>
        /// Chain an action to run when the operation finishes <b>successfully</b>; it is skipped when the operation was
        /// cancelled or faulted (use <see cref="OnComplete(Action{Operation})"/> to observe those). Attached after the
        /// operation already completed, it runs immediately if the operation succeeded. Chainable; multiple callbacks
        /// accumulate.
        /// </summary>
        public Operation OnComplete(Action onComplete)
        {
            if (onComplete == null) return this;
            if (IsCompleted)
            {
                if (Status == OperationStatus.Succeeded) InvokeSafely(onComplete);
                return this;
            }

            _onComplete += onComplete;
            return this;
        }

        /// <summary>
        /// Chain an action that runs when the operation finishes <b>whatever the outcome</b> — inspect
        /// <see cref="Status"/> / <see cref="Error"/> on the operation it receives. Attached after the operation
        /// already completed, it runs immediately. Chainable; multiple callbacks accumulate.
        /// </summary>
        public Operation OnComplete(Action<Operation> onComplete)
        {
            if (onComplete == null) return this;
            if (IsCompleted)
            {
                InvokeSafely(onComplete);
                return this;
            }

            _onFinished += onComplete;
            return this;
        }

        /// <summary>
        /// Cancel the operation. Safe to call at any time — a no-op once the operation completed or was already
        /// cancelled.
        /// </summary>
        public void Cancel()
        {
            if (IsCompleted || _source.IsCancellationRequested) return;

            _source.Cancel();
        }

        private async Task ExecuteAsync(Func<CancellationToken, Task> operation)
        {
            Exception error = null;
            bool cancelled;
            try
            {
                APSStats.RegisterOperation();
                await operation(_source.Token);
            }
            catch (Exception e)
            {
                error = e;
            }
            finally
            {
                cancelled = TaskUtils.OperationCancelled(_source.Token);
                _source.Dispose();
                APSStats.UnregisterOperation();
            }

            // Cancellation wins over a fault: an exception escaping an already-cancelled run is teardown noise, and a
            // cancellation exception from our own token is normal control flow — neither makes the operation Faulted.
            if (cancelled)
            {
                Status = OperationStatus.Cancelled;
                if (error != null && !(error is OperationCanceledException))
                    APLogger.LogException(error);
            }
            else if (error != null)
            {
                Error = error;
                Status = OperationStatus.Faulted;
                APLogger.LogException(error);
            }
            else
            {
                Status = OperationStatus.Succeeded;
            }

            Action onComplete = _onComplete;
            Action<Operation> onFinished = _onFinished;
            _onComplete = null;
            _onFinished = null;

            if (Status == OperationStatus.Succeeded && onComplete != null) InvokeSafely(onComplete);
            if (onFinished != null) InvokeSafely(onFinished);
        }

        // A throwing callback must neither skip the remaining callbacks nor vanish as an unobserved task exception.
        private void InvokeSafely(Action action)
        {
            try { action(); }
            catch (Exception e) { APLogger.LogException(e); }
        }

        private void InvokeSafely(Action<Operation> action)
        {
            try { action(this); }
            catch (Exception e) { APLogger.LogException(e); }
        }
    }
}
