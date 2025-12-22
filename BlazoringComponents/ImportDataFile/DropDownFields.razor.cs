using CRM.Shared;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.ImportDataFile
{
    public partial class DropDownFields<TItem>: ComponentBase 
    {



        [Parameter]
        public EventCallback<string> OnChangeName { get; set; }

        [Parameter]
        public string Value { get; set; }

        private List<string> _fields = null;

        
        private string _fieldName;
        

        protected override void OnInitialized()
        {
            
            var typeModel = typeof(TItem);

            _fieldName = Value;
            // Get the fields of the specified class.
            _fields = typeModel.GetProperties().Select(x=>x.Name).ToList();

            _fields.Insert(0, "");

        }


        private async Task OnChange()
        {
            await OnChangeName.InvokeAsync(_fieldName);
        }

        private void OnClick()
        {

        }

        
    }
}
