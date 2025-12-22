using Microsoft.AspNetCore.Components.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents
{
    public partial class BlazorSelectBol
    {
      
        protected override bool TryParseValueFromString(string value, out bool? result, out string validationErrorMessage)
        {

            if (bool.TryParse(value, out bool v))
            {
                validationErrorMessage = null;
                result = v;
                return true;
            }
            else
            {
                validationErrorMessage = null;
                result = null;
                return true;
            }
            
            throw new InvalidOperationException($"{GetType()} does not support the type '{typeof(bool?)}'.");
        }

    }
}
