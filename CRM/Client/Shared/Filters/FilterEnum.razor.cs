using CRM.Client.Helpers;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;

namespace CRM.Client.Shared.Filters
{
    public partial class FilterEnum<TItem>: ComponentBase where TItem : Enum
    {
        [Inject]
        IJSRuntime JSRuntime { get; set; }

        [Parameter]
        public int? Value
        {
            get => _value;
            set
            {
                _filter.Value = value.GetValueOrDefault();

                if (_value == value) 
                    return;

                _value = value;
                ValueChanged.InvokeAsync(value);
            }
        }

        [Parameter]
        public EventCallback<int?> ValueChanged { get; set; }

        [Parameter]
        public EventCallback OnClickClose { get; set; }

        [Parameter]
        public string UniqueID { get; set; }

        [Parameter]
        public string Property { get; set; }

        private int? _value;

        

        private FilterModel<int> _filter = new FilterModel<int>();

        private async void OnClickOk()
        {
            Value = _filter.Value;
            CloseFilter();
            if (OnClickClose.HasDelegate)
                await OnClickClose.InvokeAsync();
        }

        private async void OnClickCancel()
        {
            Value = null;

            CloseFilter();
            if (OnClickClose.HasDelegate)
                await OnClickClose.InvokeAsync();


        }

        private async void CloseFilter()
        {
            await JSRuntime.InvokeVoidAsync("Radzen.closePopup", $"popup{UniqueID}{Property}");
            StateHasChanged();
        }
    }
}
