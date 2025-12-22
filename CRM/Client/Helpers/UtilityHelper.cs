using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using Humanizer;
using CRM.Shared;

namespace CRM.Client.Helpers
{
    public static class UtilityHelper
    {
        public static void SetValue<C, T>(this C obj, string name, T value) where C: class
        {
            obj.GetType().GetProperty(name).SetValue(obj, value);
            
        }

        public static string GetDisplayName<TEnum>(TEnum value)
        {
            // Read the Display attribute name
            var member = value.GetType().GetMember(value.ToString())[0];
            var displayAttribute = member.GetCustomAttribute<DisplayAttribute>();
            if (displayAttribute != null)
                return displayAttribute.GetName();

            // Require the NuGet package Humanizer.Core
            // <PackageReference Include = "Humanizer.Core" Version = "2.8.26" />
            return value.ToString().Humanize();
        }

        public static T GetPropertyValue<T>(object sourceInstance, string targetPropertyName, bool throwExceptionIfNotExists = false)
        {
            string errorMsg = null;

            try
            {
                if (sourceInstance == null || string.IsNullOrWhiteSpace(targetPropertyName))
                {
                    errorMsg = $"Source object is null or property name is null or whitespace. '{targetPropertyName}'";
                    Console.WriteLine(errorMsg);

                    if (throwExceptionIfNotExists)
                        throw new ArgumentException(errorMsg);
                    else
                        return default(T);
                }

                Type returnType = typeof(T);
                Type sourceType = sourceInstance.GetType();

                PropertyInfo propertyInfo = sourceType.GetProperty(targetPropertyName);
                if (propertyInfo == null)
                {
                    errorMsg = $"Property name '{targetPropertyName}' of type '{returnType}' not found for source object of type '{sourceType}'";
                    Console.WriteLine(errorMsg);

                    if (throwExceptionIfNotExists)
                        throw new ArgumentException(errorMsg);
                    else
                        return default(T);
                }
               
                    return (T)propertyInfo.GetValue(sourceInstance, null);
            }
            catch (Exception ex)
            {
                errorMsg = $"Problem getting property name '{targetPropertyName}' from source instance.";
                Console.WriteLine(errorMsg, ex);

                if (throwExceptionIfNotExists)
                    throw;
            }

            return default(T);
        }

        public static TEnum IntToEnum<TEnum>(int i)
    where TEnum : Enum
        {
            Array array = Enum.GetValues(typeof(TEnum));
            int[] intValues = (int[])array;
            TEnum[] enumValues = (TEnum[])array;
            var b = intValues.Zip(enumValues);
            //Consider saving the dictionary to avoid recreating each time
            var c = b.ToDictionary<(int n, TEnum e), int, TEnum>(p => p.n, p => p.e);
            return c[i];//KeyNotFoundException possible here
        }

       
    }
}
