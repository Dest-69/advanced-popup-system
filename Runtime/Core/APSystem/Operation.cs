using System;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.Utils;

namespace AdvancedPS.Core.System
{
    public class Operation
    {
        private readonly Func<CancellationToken, Task> _operation;
        private Action _onComplete;
        private readonly CancellationTokenSource _source;
        private readonly Task _task;

        public Operation(Func<CancellationToken, Task> operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
            _source = TaskUtils.UpdateCancellationTokenSource(dispose: false);
            _task = ExecuteAsync();
        }

        public Operation OnComplete(Action onComplete)
        {
            _onComplete = onComplete;
            return this;
        }

        private async Task ExecuteAsync()
        {
            bool cancelled;
            try
            {
                APSStats.RegisterOperation();
                await _operation(_source.Token);
            }
            catch (Exception e)
            {
                APLogger.LogException(e);
            }
            finally 
            {
                cancelled = TaskUtils.OperationCancelled(_source.Token);
                _source.Dispose();
                APSStats.UnregisterOperation();
            }
            
            if (!cancelled) _onComplete?.Invoke();
        }

        public void Cancel()
        {
            if (_source.IsCancellationRequested) return;
            
            _source.Cancel();
            _source.Dispose();
        }
    }
}