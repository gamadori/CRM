using Microsoft.AspNetCore.Components;
using Radzen;

namespace CRM.Client.Shared.Filters
{
    
    public partial class FilterTextBox: ComponentBase
    {
        [Inject]
        DialogService DialogService { get; set; }

        [Parameter]
        public EventCallback OnClickClose { get; set; }

        [Parameter]
        public string Value
        {
            get => _value;
            set
            {
                _filter = value;
                if (_value == value) return;

                _value = value;
                ValueChanged.InvokeAsync(value);
            }
        }

        [Parameter]
        public EventCallback<string> ValueChanged { get; set; }

        private string _value = "";

        private string _filter = "";

        private void OnClickOk()
        {
            Value = _filter;
            StateHasChanged();
        }

        private async void OnClickCancel()
        {
            Value = "";
            if (OnClickClose.HasDelegate)
                await OnClickClose.InvokeAsync();

            
        }
    }
}
