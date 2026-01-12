using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using QLNet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Services
{
    public class HeaderService: IHeaderService
    {
        private readonly NavigationManager _navigationManager;
        private readonly IAGRestClientService _restClient;
        private readonly ILocalizationService _localizationService;

        // Cache per action keywords per evitare ricreazione ripetuta
        private static readonly HashSet<string> ActionKeywords = new(
            new[] { "details", "edit", "info", "new", "create", "index", "view" }, 
            StringComparer.OrdinalIgnoreCase
        );

        public HeaderService(NavigationManager navigationManager, IAGRestClientService restClient, ILocalizationService localizationService)
        {
            _navigationManager = navigationManager;
            _restClient = restClient;
            _localizationService = localizationService;
        }

        private string GetLocalizedString(string key) => _localizationService.GetLocalizedString(key);
        private bool GetLocalizedResourceNotFound(string key) => _localizationService.IsResourceNotFound(key);

        public async Task<PageHeaderModel?> Create(PageModality pageModality = PageModality.Visualization)
        {
            if (pageModality == PageModality.Child)
            {
                return new PageHeaderModel
                {
                    Title = string.Empty,
                    Subtitle = null,
                    BreadcrumbItems = new List<BreadcrumbItem>(),
                    Icon = string.Empty,
                    PageMode = pageModality,
                    DialogTitle = null
                };
            }
            var url = GetUrl();
            var segments = url.Split('?', '#')[0]
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(System.Net.WebUtility.UrlDecode)
                .ToArray();

            if (segments.Length == 0)
            {
                return CreateEmptyHeader(pageModality);
            }

            var (domainSegment, actionSegment, domainId) = ParseSegments(segments);
            var title = GetTitle(domainSegment);
            var name = await GetDomainNameAsync(domainSegment, domainId);
            var subtitle = GetSubtitle(domainSegment, actionSegment, name);
            var breadcrumbItems = await GetBreadCrumbFromUrlAsync(url);

            if (breadcrumbItems == null || !breadcrumbItems.Any())
            {
                breadcrumbItems = CreateFallbackBreadcrumb(domainSegment, name);
            }

            return new PageHeaderModel
            {
                Title = title,
                Subtitle = subtitle,
                BreadcrumbItems = breadcrumbItems,
                Icon = GetIcon(domainSegment ?? ""),
                PageMode = pageModality,
                DialogTitle = null
            };
        }

        private PageHeaderModel CreateEmptyHeader(PageModality pageModality)
        {
            return new PageHeaderModel
            {
                Title = GetLocalizedString("Home"),
                Subtitle = null,
                BreadcrumbItems = new List<BreadcrumbItem> { new(GetLocalizedString("Home"), "/") },
                PageMode = pageModality,
                Icon = GetIcon("")
            };
        }

        private (string domainSegment, string actionSegment, object? domainId) ParseSegments(string[] segments)
        {
            string domainSegment = null;
            string actionSegment = null;
            object? domainId = null;

            // Trova action se l'ultimo segmento è una keyword
            if (ActionKeywords.Contains(segments.Last()))
            {
                actionSegment = segments.Last().ToLower();
            }

            // Trova domain: ultimo segmento che rappresenta una risorsa (non numerico e non action)
            for (int i = segments.Length - 1; i >= 0; i--)
            {
                var s = segments[i];
                
                if (ActionKeywords.Contains(s) || int.TryParse(s, out _) || Guid.TryParse(s, out _))
                    continue;

                domainSegment = s.ToLower();
                domainId = ExtractDomainId(segments, i);
                break;
            }

            // Se non trovato domain, prendi primo segmento non-numerico
            if (domainSegment == null)
            {
                domainSegment = segments.FirstOrDefault(s => 
                    !int.TryParse(s, out _) && !ActionKeywords.Contains(s))?.ToLower();
            }

            return (domainSegment, actionSegment, domainId);
        }

        private object? ExtractDomainId(string[] segments, int domainIndex)
        {
            // Controlla segmento successivo
            if (domainIndex + 1 < segments.Length)
            {
                if (int.TryParse(segments[domainIndex + 1], out int idNext))
                    return idNext;
                if (Guid.TryParse(segments[domainIndex + 1], out Guid guidNext))
                    return guidNext.ToString();
            }

            // Controlla segmento precedente
            if (domainIndex - 1 >= 0)
            {
                if (int.TryParse(segments[domainIndex - 1], out int idPrev))
                    return idPrev;
                if (Guid.TryParse(segments[domainIndex - 1], out Guid guidPrev))
                    return guidPrev.ToString();
            }

            return null;
        }

        private string GetTitle(string domainSegment)
        {
            if (domainSegment == null)
                return GetLocalizedString("Home");

            return GetLocalizedResourceNotFound(domainSegment) 
                ? ToTitle(domainSegment) 
                : GetLocalizedString(domainSegment);
        }

        private async Task<string> GetDomainNameAsync(string domainSegment, object? domainId)
        {
            if (domainId == null || domainSegment == null)
                return null;

            try
            {
                return domainSegment switch
                {
                    "companies" or "company" => 
                        (await _restClient.GetItem<Company, int>((int)domainId, ConstHelper.CompaniesPath))?.RagioneSociale,
                    
                    "articles" or "article" => 
                        GetArticleName(await _restClient.GetItem<Article, int>((int)domainId, ConstHelper.ArticlesPath)),
                    
                    "products" or "product" => 
                        (await _restClient.GetItem<Product, int>((int)domainId, ConstHelper.Products))?.Name,
                    
                    "users" or "user" => 
                        (await _restClient.GetItem<ApplicationUser, string>((string)domainId, ConstHelper.UsersPath))?.NameComplete,
                    
                    "tickets" or "ticket" => 
                        (await _restClient.GetItem<Ticket, int>((int)domainId, ConstHelper.TicketPath))?.Numero,
                    
                    "contacts" or "contact" => 
                        (await _restClient.GetItem<Contact, int>((int)domainId, ConstHelper.ContactsPath))?.NameComplete,
                    
                    "deals" or "deal" => 
                        (await _restClient.GetItem<Deal, int>((int)domainId, ConstHelper.DealsPath))?.Name,
                    
                    "tickettypes" or "tickettype" => 
                        (await _restClient.GetItem<TicketType, int>((int)domainId, ConstHelper.TicketTypesPath))?.Desc,
                    
                    "tickettypeslanguages" or "tickettypeslanguage" => 
                        (await _restClient.GetItem<TicketTypeLanguage, int>((int)domainId, ConstHelper.TicketTypesLanguagesPath))?.Name,

                    "productacctypes" or "productacctype" =>
                        (await _restClient.GetItem<ProductAccessoryType, int>((int)domainId, ConstHelper.ProductAccTypesPath))?.Name,   
                    _ => null
                };
            }
            catch
            {
                // In caso di errore (es. record non trovato), restituisci null
                return null;
            }
        }

        private static string GetArticleName(Article article)
        {
            if (article == null) return null;
            return article.Product != null 
                ? $"{article.Product.Name} - {article.SerialNumber}" 
                : article.SerialNumber;
        }

        private string GetSubtitle(string domainSegment, string actionSegment, string name)
        {
            if (actionSegment != null)
            {
                return actionSegment switch
                {
                    "details" => name != null 
                        ? FormatSubTitle(domainSegment, name, false) 
                        : FormatSubTitle("DetailsSubTitle", domainSegment),
                    
                    "edit" => name != null 
                        ? FormatSubTitle(domainSegment, name, true) 
                        : FormatSubTitle("EditSubTitle", domainSegment),
                    
                    "new" or "create" => FormatSubTitle("NewSubTitle", domainSegment),
                    
                    "info" => name ?? ToTitle(actionSegment),
                    
                    "index" => FormatSubTitle("ListSubTitle", domainSegment),
                    
                    _ => null
                };
            }

            // Nessuna action esplicita
            if (name != null)
            {
                return FormatSubTitle(domainSegment ?? "", name, false);
            }

            return FormatSubTitle("ListSubTitle", domainSegment);
        }

        private string FormatSubTitle(string domine, string? name, bool edit)
        {
            if (name != null)
            {
                var subtitleType = edit ? "EditSubTitle" : "DetailsSubTitle";
                return $"{FormatSubTitle(subtitleType, domine)} {name}";
            }

            var type = edit ? "NewSubTitle" : "ListSubTitle";
            return FormatSubTitle(type, domine);
        }

        private string FormatSubTitle(string type, string domine)
        {
            string name = $"{domine}{type}";
            if (GetLocalizedResourceNotFound(name))
                return string.Format(GetLocalizedString(type), GetLocalizedString(domine));
            else
                return GetLocalizedString(name);
        }

        private List<BreadcrumbItem> CreateFallbackBreadcrumb(string domainSegment, string name)
        {
            var items = new List<BreadcrumbItem>();
            
            if (domainSegment != null)
            {
                items.Add(new BreadcrumbItem(GetLocalizedString(domainSegment), $"/{domainSegment}"));
                
                if (name != null)
                    items.Add(new BreadcrumbItem(name, null));
            }

            return items;
        }

        private string GetIcon(string domine)
        {
            return domine.ToLower() switch
            {
                "articles" => "article",
                "companies" => "business",
                "contacts" => "contact_page",
                "leads" => "emoji_people",
                "opportunities" => "trending_up",
                "settings" => "settings",
                "users" => "manage_accounts",
                "tickets" => "confirmation_number",
                "products" => "inventory_2",
                "deals" => "handshake",
                _ => "dashboard"
            };
        }

        private string GetUrl()
        {
            return _navigationManager.Uri.Substring(_navigationManager.BaseUri.Length - 1);
        }

        public async Task<List<BreadcrumbItem>> GetBreadCrumbFromCurrentUrlAsync()
        {
            var url = GetUrl();
            return await GetBreadCrumbFromUrlAsync(url);
        }

        private async Task<List<BreadcrumbItem>> GetBreadCrumbFromUrlAsync(string url)
        {
            var items = new List<BreadcrumbItem>();

            if (string.IsNullOrWhiteSpace(url))
                return items;

            var path = url.Split('?', '#')[0];
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(s => !s.Equals("details", StringComparison.OrdinalIgnoreCase) 
                         && !s.Equals("index", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (segments.Length == 0)
                return items;

            string cumulative = string.Empty;

            for (int i = 0; i < segments.Length; i++)
            {
                var segment = System.Net.WebUtility.UrlDecode(segments[i]);

                if (i > 0 && IsIdSegment(segment, out var isNumeric, out var isGuid))
                {
                    var text = await GetSegmentTextForIdAsync(segments[i - 1].ToLower(), segment, isNumeric, isGuid);
                    cumulative += "/" + segment;
                    items.Add(new BreadcrumbItem { Text = text, Url = cumulative });
                }
                else
                {
                    var text = GetLocalizedResourceNotFound(segment) 
                        ? ToTitle(segment) 
                        : GetLocalizedString(segment);

                    cumulative += "/" + segment;

                    // Ultimo segmento non cliccabile
                    var breadcrumbUrl = i == segments.Length - 1 ? null : cumulative;
                    items.Add(new BreadcrumbItem { Text = text, Url = breadcrumbUrl });
                }
            }

            return items;
        }

        private bool IsIdSegment(string segment, out bool isNumeric, out bool isGuid)
        {
            isNumeric = int.TryParse(segment, out _);
            isGuid = Guid.TryParse(segment, out _);
            return isNumeric || isGuid;
        }

        private async Task<string> GetSegmentTextForIdAsync(string previousSegment, string segment, bool isNumeric, bool isGuid)
        {
            if (!isNumeric && !isGuid)
                return segment;

            try
            {
                if (!isNumeric && !isGuid)
                    return segment;

                return previousSegment switch
                {
                    "companies" when isNumeric => 
                        (await _restClient.GetItem<Company, int>(int.Parse(segment), ConstHelper.CompaniesPath))?.RagioneSociale ?? segment,
                    
                    "articles" or "article" when isNumeric => 
                        GetArticleName(await _restClient.GetItem<Article, int>(int.Parse(segment), ConstHelper.ArticlesPath)) ?? segment,
                    
                    "products" or "product" when isNumeric => 
                        (await _restClient.GetItem<Product, int>(int.Parse(segment), ConstHelper.Products))?.Name ?? segment,
                    
                    "tickets" or "ticket" when isNumeric => 
                        (await _restClient.GetItem<Ticket, int>(int.Parse(segment), ConstHelper.TicketPath))?.Numero ?? segment,
                    
                    "users" or "user" when isGuid => 
                        (await _restClient.GetItem<ApplicationUser, string>(Guid.Parse(segment).ToString(), ConstHelper.UsersPath))?.NameComplete ?? segment,
                    
                    "users" or "user" when isNumeric => 
                        (await _restClient.GetItem<ApplicationUser, string>(segment, ConstHelper.UsersPath))?.NameComplete ?? segment,
                    
                    "contacts" or "contact" when isNumeric => 
                        (await _restClient.GetItem<Contact, int>(int.Parse(segment), ConstHelper.ContactsPath))?.NameComplete ?? segment,
                    
                    "tickettypes" or "tickettype" when isNumeric => 
                        (await _restClient.GetItem<TicketType, int>(int.Parse(segment), ConstHelper.TicketTypesPath))?.Desc ?? segment,
                    "ProductAccTypes" or "productacctype" when isNumeric =>
                        (await _restClient.GetItem<ProductAccessoryType, int>(int.Parse(segment), ConstHelper.ProductAccTypesPath))?.Name ?? segment,

                    _ => segment
                };
            }
            catch
            {
                return segment;
            }
        }

        private static string ToTitle(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || int.TryParse(value, out _))
                return value;

            var cleaned = value.Replace("-", " ").Replace("_", " ");
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleaned.ToLower());
        }
    }
}
