using Microsoft.Extensions.Localization;
using System;
using System.Linq;

namespace CRM.Client.Services
{
    /// <summary>
    /// Service for localization with case-insensitive key matching
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>
        /// Gets a localized string with case-insensitive key matching
        /// </summary>
        /// <param name="key">The resource key to localize</param>
        /// <returns>Localized string or the key if not found</returns>
        string GetLocalizedString(string key);

        /// <summary>
        /// Checks if a resource key exists (case-insensitive)
        /// </summary>
        /// <param name="key">The resource key to check</param>
        /// <returns>True if the resource is not found</returns>
        bool IsResourceNotFound(string key);
    }

    /// <summary>
    /// Implementation of localization service with case-insensitive resource lookup.
    /// This service allows you to retrieve localized strings from App.resx files
    /// without worrying about the exact casing of the key.
    /// 
    /// Example usage:
    /// <code>
    /// // Both will work even if the actual resource key is "Companies"
    /// var text1 = _localizationService.GetLocalizedString("companies");
    /// var text2 = _localizationService.GetLocalizedString("COMPANIES");
    /// var text3 = _localizationService.GetLocalizedString("Companies");
    /// </code>
    /// </summary>
    public class LocalizationService : ILocalizationService
    {
        private readonly IStringLocalizer<CRM.Shared.Resources.App> _localizer;

        public LocalizationService(IStringLocalizer<CRM.Shared.Resources.App> localizer)
        {
            _localizer = localizer;
        }

        /// <inheritdoc/>
        public string GetLocalizedString(string key)
        {
            if (string.IsNullOrEmpty(key))
                return key;

            // Try exact match first (fastest path)
            var result = _localizer[key];
            if (!result.ResourceNotFound)
                return result.Value;

            // Try case-insensitive match
            var allStrings = _localizer.GetAllStrings(includeParentCultures: true);
            var match = allStrings.FirstOrDefault(s => 
                string.Equals(s.Name, key, StringComparison.OrdinalIgnoreCase));

            if (match != null)
                return match.Value;

            // Fallback to original key
            return result.Value;
        }

        /// <inheritdoc/>
        public bool IsResourceNotFound(string key)
        {
            if (string.IsNullOrEmpty(key))
                return true;

            // Try exact match first
            var result = _localizer[key];
            if (!result.ResourceNotFound)
                return false;

            // Try case-insensitive match
            var allStrings = _localizer.GetAllStrings(includeParentCultures: true);
            return !allStrings.Any(s => string.Equals(s.Name, key, StringComparison.OrdinalIgnoreCase));
        }
    }
}
