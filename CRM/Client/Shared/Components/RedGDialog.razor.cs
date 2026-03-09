using Microsoft.AspNetCore.Components;
using Radzen;
using System.Threading.Tasks;

namespace CRM.Client.Shared.Components
{
    public partial class RedGDialog: ComponentBase
    {
        [Inject]
        DialogService DialogService { get; set; } = default!;

        public enum DialogType
        {
            None,
            Companies,
            Users,
            Contacts,
           
        }

        public enum DialogMode
        {
           
            Selection,
            Addition
        }

        [Parameter]
        public DialogType Type { get; set; } = DialogType.None;

        [Parameter]
        public DialogMode Mode { get; set; } = DialogMode.Selection;


        [Parameter]
        public EventCallback OnAddNewItem { get; set; }

        [Parameter]
        public object? IdParent { get; set; }

        


        private string _icon = "info";
        protected override async Task OnInitializedAsync()
        {

            await base.OnInitializedAsync();
        }

        private void OnSelect(int? id)
        {
            DialogService.CloseSide(id);
        }

        private void OnSelect(string? id)
        {
            DialogService.CloseSide(id);
        }

        private void OnAddingCancel()
        {
            Mode = DialogMode.Selection;
            StateHasChanged();
        }

        private void OnSaving(int ?id)
        {
            DialogService.CloseSide(id);
        }

        private void OnSaving(string id)
        {
            DialogService.CloseSide(id);
        }


        private void OnAddNew()
        {
            Mode = DialogMode.Addition;
            StateHasChanged();
        }
    }
}
