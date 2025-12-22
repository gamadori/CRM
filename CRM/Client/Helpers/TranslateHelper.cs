
using CRM.Shared;
using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Resources;

namespace CRM.Client.Helpers
{
    public static class TranslateHelper
    {
        public static string GetDisplayName(DisplayAttribute mPropAttr)
        {
            ResourceManager rm = new ResourceManager(mPropAttr.ResourceType);
            var dispName = rm.GetString(mPropAttr.Name, CultureInfo.CurrentCulture);
            return dispName;
        }

        public static string GetDisplayName<T>(string name)
        {
            var info = typeof(T).GetProperty(name);
            var value = info.GetCustomAttribute(typeof(DisplayAttribute)) as DisplayAttribute;

            if (value?.ResourceType != null)
            {
                return GetDisplayNameFromResource(value);
            }
            return value?.Name ?? name ?? "";
        }

        public static string GetDisplayName(string name, Type type)
        {
           

            var info = type.GetProperty(name);
            var value = info.GetCustomAttribute(typeof(DisplayAttribute)) as DisplayAttribute;

            if (value?.ResourceType != null)
            {
                return GetDisplayNameFromResource(value);
            }
            return value?.Name ?? name ?? "";
        }

        public static string GetDisplayName<T>(Expression<Func<T>> exp)
        {
            var expression = (MemberExpression)exp.Body;
            var value = expression.Member.GetCustomAttribute(typeof(DisplayAttribute)) as DisplayAttribute;


            if (value?.ResourceType != null)
            {
                return GetDisplayNameFromResource(value);
            }
            return value?.Name ?? expression.Member.Name ?? "";

        }

        public static string GetDisplayNameFromResource(DisplayAttribute mPropAttr)
        {
            ResourceManager rm = new ResourceManager(mPropAttr.ResourceType);
            var dispName = rm.GetString(mPropAttr.Name, CultureInfo.CurrentCulture);
            return dispName;
        }
    }
}
