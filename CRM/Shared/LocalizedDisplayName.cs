using System;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Globalization;
using System.Resources;
using System.Linq;

namespace CRM.Shared
{
    public sealed class LocalizeDisplayNameAttribute : DisplayNameAttribute
    {
        public string ResourceKey { get; }
        public string BaseName { get; set; }
        public Type ResourceType { get; set; }

        public LocalizeDisplayNameAttribute(string resourceKey)
        {
            ResourceKey = resourceKey;
        }

        public override string DisplayName
        {
            get
            {
                var baseName = BaseName;
                var assembly = ResourceType?.Assembly ?? Assembly.GetEntryAssembly();

                if (baseName == null || !baseName.Any())
                {
                    // ReSharper disable once PossibleNullReferenceException
                    baseName = $"{(ResourceType != null ? ResourceType.Namespace : assembly.GetName().Name)}.Resources";
                }


                // ReSharper disable once AssignNullToNotNullAttribute
                var res = new ResourceManager(baseName, assembly);

                var str = res.GetString(ResourceKey);

                return string.IsNullOrEmpty(str)
                    ? $"[[{ResourceKey}]]"
                    : str;
            }
        }
    }
}
