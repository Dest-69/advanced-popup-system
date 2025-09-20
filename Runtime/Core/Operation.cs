using System;
using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.Utils;
using UnityEngine;

namespace AdvancedPS.Core.System
{
    public class Operation
    {
        private readonly Func<CancellationToken, Task> _operation;
        private Action _onComplete;
        private CancellationTokenSource _source;
        private readonly Task _task;

        public Operation(Func<CancellationToken, Task> operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
            _source = UpdateCancellationTokenSource();
            _task = ExecuteAsync();
        }

        public Operation OnComplete(Action onComplete)
        {
            if (_task.IsCompleted) onComplete?.Invoke();
            else _onComplete = onComplete;
            return this;
        }

        private async Task ExecuteAsync()
        {
            bool cancelled;
            try
            {
                await _operation(_source.Token);
            }
            catch (Exception e)
            {
                APLogger.LogException(e);
            }
            finally 
            {
                cancelled = _source.IsCancellationRequested;
                _source.Dispose();
            }
            
            if (!cancelled) _onComplete?.Invoke();
        }

        public void Cancel()
        {
            _source.Cancel();
            _source.Dispose();
        }
        
        private CancellationTokenSource UpdateCancellationTokenSource()
        {
            _source?.Cancel();
            return _source = new CancellationTokenSource();
        }
    }
}