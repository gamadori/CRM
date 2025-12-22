using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CRM.Server.Helpers
{
    public static class UriHelper
    {
        public static string AbsoluteUrl(this HttpContext httpContext, string relativeUrl, object? parameters = null)
        {
            var request = httpContext.Request;

            var url = new Uri(new Uri($"{request.Scheme}://{request.Host.Value}"), relativeUrl).ToString();

            if (parameters != null)
            {
                url = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(url, ToDictionary(parameters));
            }

            return url;
        }


        private static Dictionary<string, string?> ToDictionary(object obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            var v = JsonConvert.DeserializeObject<Dictionary<string, string?>>(json);

            if (v != null)
                return v;
            else
                return new Dictionary<string, string?>();
        }
    }
}
