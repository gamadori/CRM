using Microsoft.AspNetCore.Components;
using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;
using System.Resources;
using System.Globalization;

namespace CRM.Client.Components
{
    public partial class DisplayName<T> : ComponentBase
    {
        [Parameter] public Expression<Func<T>> For { get; set; }
        [Parameter] public RenderFragment ChildContent { get; set; }

        //private string label => GetDisplayName();

        private string GetDisplayName()
        {
            
            var expression = (MemberExpression)For.Body;
            var value = expression.Member.GetCustomAttribute(typeof(DisplayAttribute), true) as DisplayAttribute;
            if (value == null)
            {
                var metaData = expression.Member.DeclaringType.GetCustomAttribute(typeof(MetadataTypeAttribute), true) as MetadataTypeAttribute;
                if (metaData != null)
                {
                    var mdProp = metaData.MetadataClassType.GetProperties()
                             .ToList()
                             .Where(p => p.Name == expression.Member.Name)
                             .FirstOrDefault();
                    if (mdProp != null)
                    {
                        var mPropAttr = mdProp.GetCustomAttribute(typeof(DisplayAttribute)) as DisplayAttribute;
                        if (mPropAttr != null)
                        {
                            if (mPropAttr.ResourceType != null)
                            {
                                return GetDisplayNameFromResource(mPropAttr);
                            }
                            //return mPropAttr.Name;
                        }
                    }
                }

            }
            if (value.ResourceType != null)
            {
                return GetDisplayNameFromResource(value);
            }
            return value?.Name ?? expression.Member.Name ?? "";
        }

        private static string GetDisplayNameFromResource(DisplayAttribute mPropAttr)
        {
            ResourceManager rm = new ResourceManager(mPropAttr.ResourceType);
            var dispName = rm.GetString(mPropAttr.Name, CultureInfo.CurrentCulture);
            return dispName;
        }
    }
}
