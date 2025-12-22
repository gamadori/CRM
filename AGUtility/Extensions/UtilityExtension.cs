using CRM.Shared;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AGUtility.Extensions
{
    public static class UtilityExtension
    {
        public static T GetPropertyValue<T>(this object sourceInstance, string targetPropertyName, bool throwExceptionIfNotExists = false)
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

        public static bool SetPropertyValue(this object sourceInstance, string targetPropertyName, object value)
        {
            string errorMsg = null;

            try
            {
                if (sourceInstance == null || string.IsNullOrWhiteSpace(targetPropertyName))
                {
                    errorMsg = $"Source object is null or property name is null or whitespace. '{targetPropertyName}'";
                    Console.WriteLine(errorMsg);


                    return false;
                }

                
                Type sourceType = sourceInstance.GetType();

                PropertyInfo propertyInfo = sourceType.GetProperty(targetPropertyName);
                if (propertyInfo == null)
                {
                   

                    return false;
                }

                propertyInfo.SetValue(sourceInstance, value);

                return true;
            }
            catch (Exception ex)
            {
                errorMsg = $"Problem getting property name '{targetPropertyName}' from source instance.";
                Console.WriteLine(errorMsg, ex);

                return false;
            }

           
        }

        public static List<TaskDependency> ToDependencyList(this IEnumerable<TaskData> data)
        {
            List<TaskDependency> list = new List<TaskDependency>();

            foreach (var t in data)
            {
                list.Add(new TaskDependency { Id = t.Id, Name = t.Name });
            }

            return list;
        }

        public static string ToListString<T>(this IEnumerable<T> data) where T: class
        {
            string s = "";

            foreach (var t in data)
            {
                if (s.Length > 0)
                    s += ",";
                
                s += (t.GetPropertyValue<int>("Id")).ToString();
            }
            return s;
        }

        public static string ToListString(this IEnumerable<int> data)
        {
            string s = "";

            foreach (var t in data)
            {
                if (s.Length > 0)
                    s += ",";

                s += t.ToString();
            }
            return s;
        }

        public static List<string> ReadAsList(this IFormFile file)
        {
            var result = new List<string>();
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                while (reader.Peek() >= 0)
                    result.Add(reader.ReadLine());
            }
            return result;
        }
    }
}

