using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AdvancedPS.Core.System
{
    public class Operation
    {
        private readonly Func<CancellationToken, Task> _operation;
        private Action _onComplete;
        private readonly CancellationTokenSource _source;

        public Operation(Func<CancellationToken, Task> operation = null, CancellationTokenSource source = null)
        {
            if (operation == null) return;
            
            _operation = operation;
            _source = source ?? new CancellationTokenSource();
            _ = ExecuteAsync();
        }

        public Operation OnComplete(Action onComplete)
        {
            _onComplete = onComplete;
            return this;
        }

        private async Task ExecuteAsync()
        {
            try
            {
                await _operation(_source.Token);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw;
            }
            
            if (!_source.Token.IsCancellationRequested)
            {
                _onComplete?.Invoke();
            }
        }

        public void Cancel()
        {
            _source.Cancel();
            _source.Dispose();
        }
    }
}