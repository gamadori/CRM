using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.Helper
{
    public static class HelperEx
    {
        public static TFieldType GetFieldValue<TFieldType, TObjectType>(this TObjectType obj, string fieldName)
        {
            var fieldInfo = obj.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic);
            return (TFieldType)fieldInfo.GetValue(obj);
        }
    }
}
