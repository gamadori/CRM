using BlazoringComponents.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents
{
    public partial class BlazorLink: BlazorButton 
    {
        
        
        

        [Parameter]
        public string Href { get; set; } = "#";

     
        

    }
}
