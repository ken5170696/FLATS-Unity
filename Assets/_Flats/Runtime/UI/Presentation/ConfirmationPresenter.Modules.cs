using UnityEngine;
namespace Flats.UI
{
    public sealed partial class ConfirmationPresenter
    {
        // Long room requirements use the same authored scroll area as other
        // confirmations. No replacement page or runtime-created graphics.
        void ResetMessageScroll()
        {
            view.RefreshMessageLayout();
        }
    }
}
