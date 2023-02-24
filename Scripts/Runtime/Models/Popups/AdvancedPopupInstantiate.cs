using System.Threading;
using System.Threading.Tasks;

namespace AdvancedPS.Core.Popup
{
    public class AdvancedPopupInstantiate : IAdvancedPopup
    {
        public override async Task Show<T>(bool deepShow = false, CancellationToken cancellationToken = default)
        {
        
        }

        public override async Task Show<T, J>(bool deepShow = false, CancellationToken cancellationToken = default)
        {
        
        }

        public override async Task Hide<T>(bool deepHide = false, CancellationToken cancellationToken = default)
        {
        
        }

        public override async Task Hide<T, J>(bool deepHide = false, CancellationToken cancellationToken = default)
        {
        
        }
    }
}
