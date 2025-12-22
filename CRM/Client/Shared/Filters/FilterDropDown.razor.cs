using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Numerics;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml;

namespace CRM.Client.Shared.Filters
{
    public partial class FilterDropDown<TItem, TValue>: ComponentBase 
    {
        [Inject]
        IJSRuntime JSRuntime { get; set; }

        [Parameter]
        public IList<TItem> Items { get; set; } = null;

        [Parameter]
        public TValue? Value
        {
            get { return _value; }
            set
            {

                

                //if (value == null && _value != null)
                //{
                //    _value = value;
                //    ValueChanged.InvokeAsync(_value);
                //}
                //else if (value != null && !value.Equals(_value))
                //{ 

                //    _value = value;
                //    ValueChanged.InvokeAsync(_value);
                    
                //}

                if (!object.Equals(_value, value))
                {
                    _value = value;
                    ValueChanged.InvokeAsync(_value);
                }
                
               
            }
        }
        [Parameter]
        public EventCallback<TValue> ValueChanged { get; set; }

       
        
        [Parameter]
        public string ValueProperty { get; set; }

        [Parameter]
        public string TextProperty { get; set; }

        [Parameter]
        public string UniqueID { get; set; }

        [Parameter]
        public string Property { get; set; }

        [Parameter]
        public EventCallback OnClickClose { get; set; }

        [Parameter]
        public EventCallback<object> Changed { get; set; }


        private TValue _value;

        private FilterModel<TValue> _filter = new FilterModel<TValue>();


        private async Task OnSelectedUserChange(object value)
        {

            if (Changed.HasDelegate)
                await Changed.InvokeAsync(value);
        }

        private async void OnClickOk()
        {
            Value = _filter.Value;
            CloseFilter();

            if (OnClickClose.HasDelegate)
                await OnClickClose.InvokeAsync();
        }

        private async void OnClickCancel()
        {
            Value = default(TValue);

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
