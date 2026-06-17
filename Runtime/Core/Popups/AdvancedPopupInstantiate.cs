using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;

namespace AdvancedPS.Core
{
    //TODO make convenient and creatable popup in real-time by show()/hide() with pool
    public class AdvancedPopupInstantiate : IAdvancedPopup
    {
        private static Operation NoOp() => new Operation(_ => Task.CompletedTask);
        
        public override void Cmd_SwitchShowHide()
        {
        }

        public override Operation SwitchShowHide(IDisplaySettings settings = null)
        {
            return NoOp();
        }

        public override Task SwitchShowHideAsync(CancellationToken token = default, IDisplaySettings settings = null)
        {
            return Task.CompletedTask;
        }

        public override Operation SwitchShowHide<T>(IDisplaySettings<T> settings = null)
        {
            return NoOp();
        }

        public override Task SwitchShowHideAsync<T>(CancellationToken token = default, IDisplaySettings<T> settings = null)
        {
            return Task.CompletedTask;
        }

        public override void Cmd_Show()
        {
        }

        public override Operation Show(IDisplaySettings settings = null)
        {
            return NoOp();
        }

        public override Task ShowAsync(CancellationToken token = default, IDisplaySettings settings = null)
        {
            return Task.CompletedTask;
        }

        public override Operation Show<T>(IDisplaySettings<T> settings = null)
        {
            return NoOp();
        }

        public override Task ShowAsync<T>(CancellationToken token = default, IDisplaySettings<T> settings = null)
        {
            return Task.CompletedTask;
        }

        public override void Cmd_Hide()
        {
        }

        public override Operation Hide(IDisplaySettings settings = null)
        {
            return NoOp();
        }

        public override Task HideAsync(CancellationToken token = default, IDisplaySettings settings = null)
        {
            return Task.CompletedTask;
        }

        public override Operation Hide<T>(IDisplaySettings<T> settings = null)
        {
            return NoOp();
        }

        public override Task HideAsync<T>(CancellationToken token = default, IDisplaySettings<T> settings = null)
        {
            return Task.CompletedTask;
        }
    }
}
