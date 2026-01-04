using CRM.Client.Models;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using static CRM.Client.Helpers.PageHelper;
using System.Globalization;
using CRM.Client.Helpers;
using System.Threading.Tasks;
using System;

namespace CRM.Client.Services
{
    public class HeaderService: IHeaderService
    {
    
        private readonly NavigationManager _navigationManager;
        private readonly IAGRestClientService _restClient;
        private readonly ILocalizationService _localizationService;

        public HeaderService(NavigationManager navigationManager, IAGRestClientService restClient, ILocalizationService localizationService)
        {
           
            _navigationManager = navigationManager;
            _restClient = restClient;
            _localizationService = localizationService;
        }

        // Helper method to get localized string with case-insensitive fallback
        private string GetLocalizedString(string key) => _localizationService.GetLocalizedString(key);

        private bool GetLocalizedResourceNotFound(string key) => _localizationService.IsResourceNotFound(key);

        


        // New overload: build header from current URL only
        public async Task<PageHeaderModel> Create(PageModality pageModality = PageModality.Visualization)
        {
            var url = GetUrl();
            var segments = url.Split('?', '#')[0].Split('/', System.StringSplitOptions.RemoveEmptyEntries).Select(s => System.Net.WebUtility.UrlDecode(s)).ToArray();

            // determine action keywords
            var actions = new HashSet<string>(new[] { "details", "edit", "info", "new", "create", "index", "view" }, System.StringComparer.OrdinalIgnoreCase);

            string domainSegment = null;
            string actionSegment = null;
            object? domainId = null;

            if (segments.Length == 0)
            {
                // fallback to Home
                var modelEmpty = new PageHeaderModel()
                {
                    Title = GetLocalizedString("Home"),
                    Subtitle = null,
                    BreadcrumbItems = new List<BreadcrumbItem>() { new BreadcrumbItem(GetLocalizedString("Home"), "/") },
                    PageMode = pageModality,
                    Icon = GetIcon("")
                };
                return modelEmpty;
            }

            // find action if last segment is action word
            if (actions.Contains(segments.Last()))
            {
                actionSegment = segments.Last().ToLower();
            }

            // find domain: the last segment that represents a resource name (not numeric and not action)
            for (int i = segments.Length - 1; i >= 0; i--)
            {
                var s = segments[i];
                if (actions.Contains(s))
                    continue;

                if (int.TryParse(s, out _))
                    continue;

                if (Guid.TryParse(s, out _))
                    continue;

                domainSegment = s.ToLower();

                // attempt to find id for this domain: check next segment (i+1) or previous (i-1)
                if (i + 1 < segments.Length && int.TryParse(segments[i + 1], out int idNext))
                {
                    domainId = idNext;
                }
                else if (i - 1 >= 0 && int.TryParse(segments[i - 1], out int idPrev))
                {
                    domainId = idPrev;
                }
                else if (i + 1 < segments.Length && Guid.TryParse(segments[i + 1], out Guid guidNext))
                {
                    domainId = guidNext.ToString();
                }
                else if (i - 1 >= 0 && Guid.TryParse(segments[i - 1], out Guid guidPrev))
                {
                    domainId = guidPrev.ToString();
                }
                break;
            }

            // if no domain found, pick first non-numeric segment
            if (domainSegment == null)
            {
                foreach (var s in segments)
                {
                    if (!int.TryParse(s, out _) && !actions.Contains(s))
                    {
                        domainSegment = s.ToLower();
                        break;
                    }
                }
            }

            // determine display title
            var title = domainSegment != null ? (GetLocalizedResourceNotFound(domainSegment) ? ToTitle(domainSegment) : GetLocalizedString(domainSegment)) : GetLocalizedString("Home");

            // determine name for subtitle when possible
            string name = null;
            if (domainId != null && domainSegment != null)
            {
                switch (domainSegment)
                {
                    case "companies":
                    case "company":
                        var comp = await _restClient.GetItem<Company, int>((int)domainId, ConstHelper.CompaniesPath);
                        if (comp != null)
                            name = comp.RagioneSociale;
                        break;
                    case "articles":
                    case "article":
                        var art = await _restClient.GetItem<Article, int>((int)domainId, ConstHelper.ArticlesPath);
                        if (art != null)
                            name = art.Product != null ? $"{art.Product.Name} - {art.SerialNumber}" : art.SerialNumber;
                        break;
                    case "products":
                    case "product":
                        var prod = await _restClient.GetItem<Product, int>((int)domainId, ConstHelper.Products);
                        if (prod != null)
                            name = prod.Name;
                        break;
                    case "users":
                    case "user":
                        var usr = await _restClient.GetItem<ApplicationUser, string>((string)domainId, ConstHelper.UsersPath).ConfigureAwait(false) as ApplicationUser;
                        // note: GetItem generic expects K type; users use string id normally — fallback
                        if (usr != null)
                            name = usr.NameComplete;
                        break;
                    case "tickets":
                    case "ticket":
                        var tick = await _restClient.GetItem<Ticket, int>((int)domainId, ConstHelper.TicketPath);
                        if (tick != null)
                            name = tick.Numero;
                        break;
                    case "contacts":
                    case "contact":
                        var cont = await _restClient.GetItem<Contact, int>((int)domainId, ConstHelper.ContactsPath);
                        if (cont != null)
                            name = cont.NameComplete;
                        break;
                    case "deals":
                    case "deal":
                        var deal = await _restClient.GetItem<Deal, int>((int)domainId, ConstHelper.DealsPath);
                        if (deal != null)
                            name = deal.Name;
                        break;
                    case "tickettypes":
                    case "tickettype":
                        var ttype = await _restClient.GetItem<TicketType, int>((int)domainId, ConstHelper.TicketTypesPath);
                        if (ttype != null)
                            name = ttype.Desc;
                        break;
                    case "tickettypeslanguages":
                    case "tickettypeslanguage":
                        var ttlang = await _restClient.GetItem<TicketTypeLanguage, int>((int)domainId, ConstHelper.TicketTypesLanguagesPath);
                        if (ttlang != null)
                            name = ttlang.Name;
                        break;
                    default:
                        // no known mapping: leave name null
                        break;
                }
            }

            // determine edit flag and subtitle text
            bool edit = false;
            string subtitle = null;

            if (actionSegment != null)
            {
                switch (actionSegment)
                {
                    case "details":
                        edit = false;
                        subtitle = name != null ? GetSubTitle(domainSegment ?? "", name, false) : FormatSubTitle("DetailsSubTitle", domainSegment);  //_localizer[$"{(domainSegment ?? "")}DetailsSubTitle"];
                        break;
                    case "edit":
                        edit = true;
                        subtitle = name != null ? GetSubTitle(domainSegment ?? "", name, true) : FormatSubTitle("EditSubTitle", domainSegment); // _localizer[$"{(domainSegment ?? "")}EditSubTitle"];
                        break;
                    case "new":
                    case "create":
                        edit = true;
                        subtitle = FormatSubTitle("NewSubTitle", domainSegment);  //_localizer[$"{(domainSegment ?? "")}NewSubTitle"];
                        break;
                    case "info":
                        edit = false;
                        subtitle = name ?? ToTitle(actionSegment);
                        break;
                    case "index":
                        edit = false;
                        subtitle = FormatSubTitle("ListSubTitle", domainSegment); // _localizer[$"{(domainSegment ?? "")}ListSubTitle"];
                        break;
                    default:
                        subtitle = null;
                        break;
                }
            }
            else
            {
                // no explicit action: if there's an id assume details
                if (domainId != null)
                {
                    edit = false;
                    subtitle = name != null ? GetSubTitle(domainSegment ?? "", name, false) : null;
                }
                else
                {
                    // list/index
                    subtitle = GetSubTitle(domainSegment, null, false); // _localizer[$"{(domainSegment ?? "")}ListSubTitle"];
                }
            }

            // build breadcrumb
            var breadcrumbItems = await GetBreadCrumbFromUrlAsync(url);

            // if breadcrumb empty, fallback to simple
            if (breadcrumbItems == null || !breadcrumbItems.Any())
            {
                breadcrumbItems = new List<BreadcrumbItem>();
                if (domainSegment != null)
                {
                    breadcrumbItems.Add(new BreadcrumbItem(GetLocalizedString(domainSegment), $"/{domainSegment}"));
                    if (name != null)
                        breadcrumbItems.Add(new BreadcrumbItem(name, null));
                }
            }

            var header = new PageHeaderModel()
            {
                Title = title,
                Subtitle = subtitle,
                BreadcrumbItems = breadcrumbItems,
                Icon = GetIcon(domainSegment ?? ""),
                PageMode = pageModality,
                DialogTitle = null
            };

            return header;
        }

        private string FormatSubTitle(string domine, string? name, bool edit)
        {
            if (name != null)
            {
                return edit ? $"{FormatSubTitle("EditSubTitle", domine)} {name}" : $"{FormatSubTitle("DetailsSubTitle", domine)} {name}";
                
            }
            else
                return edit ? $"{FormatSubTitle("NewSubTitle", domine)} {name}" : $"{FormatSubTitle("ListSubTitle", domine)} {name}";
            
        }

        private string FormatSubTitle(string type, string domine)
        {
            return string.Format(GetLocalizedString(type), GetLocalizedString(domine));
        }

        private List<BreadcrumbItem> BreadCrumbRoot(List<string> root)
        {
           
            List<BreadcrumbItem> items = new List<BreadcrumbItem>();
            if (root != null && root.Any())
            {
                foreach (var parent in root)
                {
                    items.Add(new BreadcrumbItem() { Text = GetLocalizedString(parent), Url = parent });
                }
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
                _ => "dashboard"
            };
        }
        
        
        private string GetSubTitle(string domine, string? name, bool edit)
        {
            
           
                return FormatSubTitle(domine, name, edit);

                //return edit ? $"{_localizer[$"{domine}EditSubTitle"]} {name}" : $"{_localizer[$"{domine}DetailsSubTitle"]} {name}";
           
            // return edit ? _localizer[$"{domine}NewSubTitle"] : _localizer[$"{domine}ListSubTitle"];
        }

        private string? GetDialogTitle(string domine, bool edit, object? id)
        {
            if (edit)
            {
                return id == null ? string.Format(GetLocalizedString("NewItem"), GetLocalizedString(domine)) : string.Format(GetLocalizedString("EditItem"), GetLocalizedString(domine));
            }
            return null;
        }

        private string GetUrl()
        {
            string uri;


            uri = _navigationManager.Uri.Substring(_navigationManager.BaseUri.Length - 1);

            
            return uri;

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

            // strip query and fragment
            var path = url.Split('?', '#')[0];

            var segments = path.Split('/', System.StringSplitOptions.RemoveEmptyEntries);

            if (segments == null || segments.Length == 0)
                return items;
            
            // Eliminare i segment contenenti "Details" e "Index"
            
            segments = segments.Where(s => !s.Equals("details", StringComparison.OrdinalIgnoreCase)
                                        && !s.Equals("index", StringComparison.OrdinalIgnoreCase)
                                        ).ToArray();

            string cumulative = string.Empty;


            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];

                // decode URL-encoded parts
                segment = System.Net.WebUtility.UrlDecode(segment);

                // if segment is numeric or a guid treat as id of previous resource
                if (i > 0 && (int.TryParse(segment, out int numericId) || Guid.TryParse(segment, out Guid guidId)))
                {
                    var prev = segments[i - 1].ToLower();
                    string text = segment;

                    switch (prev)
                    {
                        case "companies":
                            if (int.TryParse(segment, out numericId))
                            {
                                var company = await _restClient.GetItem<Company, int>(numericId, ConstHelper.CompaniesPath);
                                text = company?.RagioneSociale ?? numericId.ToString();
                            }
                            break;
                        case "articles":
                        case "article":
                            if (int.TryParse(segment, out numericId))
                            {
                                var article = await _restClient.GetItem<Article, int>(numericId, ConstHelper.ArticlesPath);
                                if (article != null)
                                    text = article.Product != null ? $"{article.Product.Name} - {article.SerialNumber}" : article.SerialNumber;
                                else
                                    text = numericId.ToString();
                            }
                            break;
                        case "products":
                        case "product":
                            if (int.TryParse(segment, out numericId))
                            {
                                var product = await _restClient.GetItem<Product, int>(numericId, ConstHelper.Products);
                                text = product?.Name ?? numericId.ToString();
                            }
                            break;

                        case "tickets":
                        case "ticket":
                            if (int.TryParse(segment, out numericId))
                            {
                                var ticket = await _restClient.GetItem<Ticket, int>(numericId, ConstHelper.TicketPath);
                                text = ticket?.Numero ?? numericId.ToString();
                            }
                            break;
                    
                        case "users":
                        case "user":
                            // users ids are GUID/string typed
                            if (Guid.TryParse(segment, out guidId))
                            {
                                var user = await _restClient.GetItem<ApplicationUser, string>(guidId.ToString(), ConstHelper.UsersPath);
                                if (user != null)
                                    text = user.NameComplete;
                                else
                                    text = guidId.ToString();
                            }
                            else if (int.TryParse(segment, out numericId))
                            {
                                // fallback if numeric id used
                                var user = await _restClient.GetItem<ApplicationUser, string>(numericId.ToString(), ConstHelper.UsersPath);
                                text = user?.NameComplete ?? numericId.ToString();
                            }
                            break;
                        case "contacts":
                        case "contact":
                            if (int.TryParse(segment, out numericId))
                            {
                                var contact = await _restClient.GetItem<Contact, int>(numericId, ConstHelper.ContactsPath);
                                text = contact?.NameComplete ?? numericId.ToString();
                            }
                            break;
                        case "tickettypes":
                        case "tickettype":
                            if (int.TryParse(segment, out numericId))
                            {
                                var ticketType = await _restClient.GetItem<TicketType, int>(numericId, ConstHelper.TicketTypesPath);
                                text = ticketType?.Desc ?? numericId.ToString();
                            }
                            break;
                        default:
                            text = segment;
                            break;
                    }

                    cumulative += "/" + segment;

                    // make id item clickable
                    items.Add(new BreadcrumbItem { Text = text, Url = cumulative });
                }
                else
                {
                    // normal segment
                    

                    string text = !GetLocalizedResourceNotFound(segment) ? GetLocalizedString(segment) : ToTitle(segment);

                    cumulative += "/" + segment;

                    // last segment -> not clickable
                    if (i == segments.Length - 1)
                        items.Add(new BreadcrumbItem { Text = text, Url = null });
                    else
                        items.Add(new BreadcrumbItem { Text = text, Url = cumulative });
                }
            }

            return items;
        }

        private static string ToTitle(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            // if numeric keep as-is
            if (int.TryParse(value, out _))
                return value;

            var cleaned = value.Replace("-", " ").Replace("_", " ");
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleaned.ToLower());
        }
    }
}
