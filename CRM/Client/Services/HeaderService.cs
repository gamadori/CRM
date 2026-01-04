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
        private readonly IStringLocalizer<CRM.Shared.Resources.App> _localizer;

        private readonly NavigationManager _navigationManager;
        private readonly IAGRestClientService _restClient;
        public HeaderService(IStringLocalizer<CRM.Shared.Resources.App> localizer, NavigationManager navigationManager, IAGRestClientService restClient)
        {
            _localizer = localizer;
            _navigationManager = navigationManager;
            _restClient = restClient;
        }

        

        public PageHeaderModel Create(string domine, object? id = null, string? name = null, bool edit = false, string urlbase = null, string? subTitle = null, 
            PageModality pageModality = PageModality.Visualization)
        {
            List<string> root = new List<string>();

            PageHeaderModel headerModel = new PageHeaderModel
            {
                Title = _localizer[domine],
            };

            if (urlbase == null)
            {
                urlbase = $"/{domine}";
            }

            if (urlbase != null)
            {
                var rootDomine = urlbase.Split('/');

                foreach (var item in rootDomine)
                {
                    if (item == "" || item.ToLower() == domine.ToLower())
                    {
                        continue;
                    }
                    root.Add(item);
                }
            }

            headerModel.BreadcrumbItems = BreadCrumbRoot(root);

            headerModel.BreadcrumbItems.Add(
            
                new BreadcrumbItem(_localizer[domine], urlbase)
            );


            if (id != null)
            {
                headerModel.BreadcrumbItems.Add(new BreadcrumbItem()
                {
                    Text = name,
                    Url = $"{urlbase}/Details/{id}"
                });
            }
            else if (edit)
            {
                headerModel.BreadcrumbItems.Add(new BreadcrumbItem()
                {
                    Text = _localizer[$"New {domine}"],
                    Url = null
                });
            }
            headerModel.Icon = GetIcon(domine);
            headerModel.Subtitle = subTitle ?? GetSubTitle(domine, name, edit);
            headerModel.PageMode = pageModality;
            headerModel.DialogTitle = GetDialogTitle(domine, edit, id);
            return headerModel;
        }

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
                    Title = _localizer["Home"],
                    Subtitle = null,
                    BreadcrumbItems = new List<BreadcrumbItem>() { new BreadcrumbItem(_localizer["Home"], "/") },
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
            var title = domainSegment != null ? (_localizer[domainSegment].ResourceNotFound ? ToTitle(domainSegment) : _localizer[domainSegment]) : _localizer["Home"];

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
                        subtitle = name != null ? GetSubTitle(domainSegment ?? "", name, false) : _localizer[$"{(domainSegment ?? "")}DetailsSubTitle"];
                        break;
                    case "edit":
                        edit = true;
                        subtitle = name != null ? GetSubTitle(domainSegment ?? "", name, true) : _localizer[$"{(domainSegment ?? "")}EditSubTitle"];
                        break;
                    case "new":
                    case "create":
                        edit = true;
                        subtitle = _localizer[$"{(domainSegment ?? "")}NewSubTitle"];
                        break;
                    case "info":
                        edit = false;
                        subtitle = name ?? ToTitle(actionSegment);
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
                    subtitle = _localizer[$"{(domainSegment ?? "")}ListSubTitle"];
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
                    breadcrumbItems.Add(new BreadcrumbItem(_localizer[domainSegment], $"/{domainSegment}"));
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

        private List<BreadcrumbItem> BreadCrumbRoot(List<string> root)
        {
           
            List<BreadcrumbItem> items = new List<BreadcrumbItem>();
            if (root != null && root.Any())
            {
                foreach (var parent in root)
                {
                    items.Add(new BreadcrumbItem() { Text = _localizer[parent], Url = parent });
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
            
            if (name != null)
            {
                return edit ? $"{_localizer[$"{domine}EditSubTitle"]} {name}" : $"{_localizer[$"{domine}DetailsSubTitle"]} {name}";
            }
            else
                return edit ? _localizer[$"{domine}NewSubTitle"] : _localizer[$"{domine}ListSubTitle"];
        }

        private string? GetDialogTitle(string domine, bool edit, object? id)
        {
            if (edit)
            {
                return id == null ? string.Format(_localizer["NewItem"], _localizer[domine]) : string.Format(_localizer["EditItem"], _localizer[domine]);
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
                    var localized = _localizer[segment];
                    string text = !localized.ResourceNotFound ? localized.Value : ToTitle(segment);

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
