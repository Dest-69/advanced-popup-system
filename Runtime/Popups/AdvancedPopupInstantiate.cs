using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;

namespace AdvancedPS.Core
{
    public class AdvancedPopupInstantiate : IAdvancedPopup
    {
        public override void Cmd_Show()
        {
            return;
        }

        public override Operation Show(BaseSettings settings = null)
        {
            return null;
        }

        public override Task ShowAsync(CancellationToken token = default, BaseSettings settings = null)
        {
            return Task.CompletedTask;
        }

        public override Operation Show<T>(BaseSettings settings = null)
        {
            return null;
        }

        public override Task ShowAsync<T>(CancellationToken token = default, BaseSettings settings = null)
        {
            return Task.CompletedTask;
        }

        public override void Cmd_Hide()
        {
            return;
        }

        public override Operation Hide(BaseSettings settings = null)
        {
            return null;
        }

        public override Task HideAsync(CancellationToken token = default, BaseSettings settings = null)
        {
            return Task.CompletedTask;
        }

        public override Operation Hide<T>(BaseSettings settings = null)
        {
            return null;
        }

        public override Task HideAsync<T>(CancellationToken token = default, BaseSettings settings = null)
        {
            return Task.CompletedTask;
        }
    }
}
