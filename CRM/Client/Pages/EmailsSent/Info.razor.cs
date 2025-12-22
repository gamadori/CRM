using Microsoft.AspNetCore.Components;
using QLNet;
using System.Threading.Tasks;

namespace CRM.Client.Pages.EmailsSent
{
    public partial class Info: ComponentBase
    {
        public enum PartialViews
        {
            Index,
            Details,
            Edit,
            New

        }

        private PartialViews _partialViews;

        private int _idEmail;
        protected override void  OnInitialized()
        {

            _partialViews = PartialViews.Index;
            base.OnInitialized();

        }

        private void OnClickDetails(int idEmail)
        {
            _idEmail = idEmail;
            _partialViews = PartialViews.Details;
            StateHasChanged();
        }

        private void OnDetailsClose()
        {
            _partialViews = PartialViews.Index;
            StateHasChanged();
        }
    }
}
