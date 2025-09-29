using System.Threading;
using System.Threading.Tasks;
using AdvancedPS.Core.System;

namespace AdvancedPS.Core
{
    //TODO make convenient and creatable popup in real-time by show()/hide() with pool
    public class AdvancedPopupInstantiate : IAdvancedPopup
    {
        public override void Cmd_Show()
        {
            return;
        }

        public override Operation Show(IDisplaySettings settings = null)
        {
            return null;
        }

        public override Task ShowAsync(CancellationToken token = default, IDisplaySettings settings = null)
        {
            return Task.CompletedTask;
        }

        public override Operation Show<T>(IDisplaySettings<T> settings = null)
        {
            return null;
        }

        public override Task ShowAsync<T>(CancellationToken token = default, IDisplaySettings<T> settings = null)
        {
            return Task.CompletedTask;
        }

        public override void Cmd_Hide()
        {
            return;
        }

        public override Operation Hide(IDisplaySettings settings = null)
        {
            return null;
        }

        public override Task HideAsync(CancellationToken token = default, IDisplaySettings settings = null)
        {
            return Task.CompletedTask;
        }

        public override Operation Hide<T>(IDisplaySettings<T> settings = null)
        {
            return null;
        }

        public override Task HideAsync<T>(CancellationToken token = default, IDisplaySettings<T> settings = null)
        {
            return Task.CompletedTask;
        }
    }
}
