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
    }
}
