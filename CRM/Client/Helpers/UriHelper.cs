using AGUtility.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace CRM.Client.Helpers
{
    public static class UriHelper
    {
        public static Uri GetUriWithparameters(this Uri uri, Dictionary<string, string> queryParams = null, int port = -1)
        {
            var builder = new UriBuilder(uri);
            builder.Port = port;
            if (null != queryParams && 0 < queryParams.Count)
            {
                var query = HttpUtility.ParseQueryString(builder.Query);
                foreach (var item in queryParams)
                {
                    query[item.Key] = item.Value;
                }
                builder.Query = query.ToString();
            }
            return builder.Uri;
        }

        public static string GetUriWithparameters(string uri, Dictionary<string, string> queryParams = null, int port = -1)
        {
            var builder = new UriBuilder(uri);
            builder.Port = port;
            if (null != queryParams && 0 < queryParams.Count)
            {
                var query = HttpUtility.ParseQueryString(builder.Query);
                foreach (var item in queryParams)
                {
                    query[item.Key] = item.Value;
                }
                builder.Query = query.ToString();
            }
            return builder.Uri.ToString();
        }

        public static string GetQueryString(this Uri uri, Dictionary<string, string> queryParams = null)
        {
            var query = HttpUtility.ParseQueryString(uri.Query); 
            foreach (var item in queryParams)
            {
                query[item.Key] = item.Value;
            }
            return query.ToString();
        }
        public static string BuildQueryString(Dictionary<string, string> queryStringParams)
        {
            List<string> paramList = new List<string>();
            foreach (var parameter in queryStringParams)
            {
               
                paramList.Add(parameter.Key + "=" + parameter.Value);
            }
            return "?" + string.Join("&", paramList);
        }
        public static string BuildQueryString(Dictionary<string, object?> queryStringParams)
        {
            List<string> paramList = new List<string>();
            foreach (var parameter in queryStringParams)
            {
                string value;
                if (parameter.Value != null)
                {
                    if (parameter.Value.GetType() == typeof(DateTime) || parameter.Value.GetType() == typeof(DateTime?))
                        value = ((DateTime)parameter.Value).ToString("yyyy-MM-dd");
                    else
                        value = parameter.Value.ToString();
                    paramList.Add(parameter.Key + "=" + value);
                }
            }
            return "?" + string.Join("&", paramList);
        }

        /// <summary>
        /// Creates a query string from the properties of the specified object.
        /// </summary>
        /// <remarks>The method converts each property of the object into a query string parameter. 
        /// DateTime properties are formatted as "yyyy-MM-dd". Null property values are represented as empty
        /// strings.</remarks>
        /// <typeparam name="F">The type of the object whose properties are used to create the query string.</typeparam>
        /// <param name="data">The object containing properties to be converted into query string parameters. Cannot be null.</param>
        /// <returns>A query string representing the object's properties and their values. The query string is empty if the
        /// object has no properties.</returns>
        public static string CreateQueryString<F>(F data)
        {
            if (data == null)
                return string.Empty;

            Dictionary<string, string> param = new Dictionary<string, string>();

            Type returnType = data.GetType();
            var fields = returnType.GetProperties();

            foreach (var field in fields)
            {
                string value;
                var obj = UtilityHelper.GetPropertyValue<object>(data, field.Name);

                if (field.PropertyType == typeof(DateTime) || (field.PropertyType == typeof(DateTime?) && obj != null))
                    value = ((DateTime)obj).ToString("yyyy-MM-dd");
                else
                    value = obj?.ToString() ?? "";

                param.Add(field.Name, value);
            }

            var qs = UriHelper.BuildQueryString(param);

            return qs;
        }
    }
}

