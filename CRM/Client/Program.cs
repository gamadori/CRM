using CRM.Client.Handlers;
using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.Resources;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace CRM.Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            // Registra il LanguageHandler PRIMA di AddHttpClient
            builder.Services.AddScoped<LanguageHandler>();

            builder.Services.AddHttpClient("CRM.ServerAPI", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
                .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>()
                .AddHttpMessageHandler<LanguageHandler>();

            // HttpClient senza autenticazione per pagine pubbliche (es. ConfirmSignature)
            builder.Services.AddHttpClient("CRM.PublicAPI", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));

            // Supply HttpClient instances that include access tokens when making requests to the server project
            // IMPORTANTE: Usa SOLO l'HttpClient configurato con IHttpClientFactory, NON crearne uno nuovo
            builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("CRM.ServerAPI"));

            builder.Services.AddApiAuthorization()
                .AddAccountClaimsPrincipalFactory<RolesClaimsPrincipalFactory>();

            builder.Services.AddAuthorizationCore(config =>
            {
                foreach (var r in Enum.GetValues(typeof(ePolicy)))
                {
                    config
                    .AddPolicy(r.ToString(),
                        policy => policy.RequireRole(PolicyRoles.vPoliyRoles[(int)r]));
                }
            });

            builder.Services.AddTransient<ISettingsService<SmtpSetting>, SettingsService<SmtpSetting>>(sp =>
            {
                return new SettingsService<SmtpSetting>(sp.GetRequiredService<HttpClient>(), ConstHelper.SmtpSettingsPath);
            });


            builder.Services.AddMemoryCache();

            builder.Services.AddTransient<ITicketsService, ProxyTicketsService>();

            builder.Services.AddTransient<ITicketChatsService, TicketChatsService>();

            builder.Services.AddTransient<ChatService, ChatService>();

            builder.Services.AddTransient<IBreadCrumbService, BreadCrumbService>();

            builder.Services.AddTransient<IInterventionTypesService, ProxyInterventionTypesService>();

            builder.Services.AddTransient<IManyToManyService<UserGroupModel>, GroupUsersService>();
            builder.Services.AddTransient<Radzen.DialogService>();
            builder.Services.AddTransient<INavMenuService, NavMenuService>();
            builder.Services.AddTransient<IDealService, DealService>();
            builder.Services.AddTransient<IManyToManyService<ProductParentChildModel>, ProductParentChildService>();
            builder.Services.AddScoped<IEnumService, EnumService>();
            builder.Services.AddScoped<IAccessoryTypesService, AccessoryTypesService>();
            builder.Services.AddScoped<IAccessoryTypeLanguagesService, AccessoryTypeLanguagesService>();
            builder.Services.AddScoped<IAccessoriesService, AccessoriesService>();
            builder.Services.AddScoped<IProductAccTypesService, ProductAccTypesService>();
            builder.Services.AddScoped<IProductAccTypesLangService, ProductAccTypesLangService>();
            builder.Services.AddScoped<IContractTypesService, ContractTypesService>();
            builder.Services.AddScoped<ITicketTypesService, TicketTypesService>();
            builder.Services.AddScoped<ITicketTypeLanguageService, TicketTypeLanguageService>();
            builder.Services.AddScoped<IUserService, UsersService>();
           
            builder.Services.AddScoped<IContractTypeTicketService, ContractTypeTicketService>();
            builder.Services.AddScoped<ICompanyContractsService, CompanyContractsService>();
            builder.Services.AddScoped<IAGRestClientService, AGRestClientService>();
            builder.Services.AddScoped<IProjectsService, ProjectsService>();
            builder.Services.AddScoped<ILanguagesService, ProxyLanguagesService>();
            builder.Services.AddScoped<ISmtpSettingsService, ProxySmtpSettingsService>();
            builder.Services.AddScoped<ILocalizationService, LocalizationService>();
            builder.Services.AddScoped<IHeaderService, HeaderService>();
            builder.Services.AddScoped<ILogosService, ProxyLogosService>();
            builder.Services.AddScoped<ILogEventService, ProxyLogEventService>();
            builder.Services.AddScoped<ITicketStatesService, ProxyTicketsStates>();
            builder.Services.AddScoped<ITicketInterventionsService, ProxyTicketInterventionsService>();
            builder.Services.AddScoped<IBaseRestService<TicketIntervention, TicketInterventionFilter, int>>(sp =>
                sp.GetRequiredService<ITicketInterventionsService>());
            builder.Services.AddScoped<ITicketInterventionUsersService, ProxyTicketInterventionUsersService>();
            builder.Services.AddScoped<IFoldersService, ProxyFoldersService>();
            builder.Services.AddScoped<IInterventionTypeLangsService, ProxyInterventionTypeLangsService>();
            builder.Services.AddScoped<IFolderLanguagesService, ProxyFolderLanguagesService>();
            builder.Services.AddScoped<IAttachmentsService, ProxyAttachmentsService>();
            builder.Services.AddScoped<IProductCatalogAssetsService, ProxyProductCatalogAssetsService>();
            builder.Services.AddScoped<IProductCatalogService, ProxyProductCatalogService>();
            builder.Services.AddScoped<IProductsService, ProxyProductsService>();
            builder.Services.AddScoped<IProductTypesService, ProxyProductTypesService>();
            builder.Services.AddScoped<ICompaniesService, ProxyCompaniesService>();
            builder.Services.AddScoped<IContactsService, ProxyContactsService>();
            builder.Services.AddScoped<ITicketFeedbackService, ProxyTicketFeedbackService>();
            builder.Services.AddScoped<IArticlesService, ProxyArticlesService>();

            builder.Services.AddTransient<IManyToManyService<TicketTypeUser>, ManyToManyService<TicketTypeUser>>(sp =>
            {
                return new ManyToManyService<TicketTypeUser>(sp.GetRequiredService<HttpClient>(), ConstHelper.TicketTypesUsersPath);
            });

            builder.Services.AddTransient<IManyToManyService<TicketTypeGroup>, ManyToManyService<TicketTypeGroup>>(sp =>
            {
                return new ManyToManyService<TicketTypeGroup>(sp.GetRequiredService<HttpClient>(), ConstHelper.TicketTypesGroupsPath);
            });

            builder.Services.AddTransient<IRestService<ApplicationUser>>(sp =>
            {
                return new RestGetClientService<ApplicationUser>(sp.GetRequiredService<HttpClient>(), ConstHelper.UserSignedPath);
            });

            builder.Services.AddTransient<IReportService<TicketDashBoardModel, TicketDashBoardModelFilter>>(sp =>
            {
                return new ReportService<TicketDashBoardModel, TicketDashBoardModelFilter>(sp.GetRequiredService<HttpClient>(), ConstHelper.TicketsDashboardPath);
            });

            builder.Services.AddTransient<IBaseRestService<Attachment, AttachmentsFilter, int>, RestClientService<Attachment, AttachmentsFilter, int>>(sp =>
            {
                return new RestClientService<Attachment, AttachmentsFilter, int>(sp.GetRequiredService<HttpClient>(), ConstHelper.AttachmentsPath);
            });

            builder.Services.AddTransient<IBaseRestService<InterventionType, InterventionTypeFilter, int>,
               RestClientService<InterventionType, InterventionTypeFilter, int>>(sp =>
               {
                   return new RestClientService<InterventionType, InterventionTypeFilter, int>(sp.GetRequiredService<HttpClient>(),
                        ConstHelper.InterventionTypesPath);
               });

            builder.Services.AddTransient<IBaseRestService<InterventionTypeLanguage, InterventionTypeLangFilter, int>,
              RestClientService<InterventionTypeLanguage, InterventionTypeLangFilter, int>>(sp =>
              {
                  return new RestClientService<InterventionTypeLanguage, InterventionTypeLangFilter, int>(sp.GetRequiredService<HttpClient>(),
                       ConstHelper.InterventionTypeLangsPath);
              });

            builder.Services.AddTransient<IBaseRestService<EmailSent, EmailSentFilterModel, int>, RestClientService<EmailSent, EmailSentFilterModel, int>>(sp =>
            {
                return new RestClientService<EmailSent, EmailSentFilterModel, int>(sp.GetRequiredService<HttpClient>(), ConstHelper.EmailsSentPath);
            });


            builder.Services.AddTransient<IIdentityRestService, IdentityRestClientService>(sp =>
            {
                return new IdentityRestClientService(sp.GetRequiredService<HttpClient>(), ConstHelper.UsersPath);
            });


           
            builder.Services.AddTransient<IBaseRestService<ApplicationUser, UsersFilterModel, string>, RestClientService<ApplicationUser, UsersFilterModel, string>>(sp =>
            {
                return new RestClientService<ApplicationUser, UsersFilterModel, string>(sp.GetRequiredService<HttpClient>(), ConstHelper.UsersPath);
            });

            
            builder.Services.AddScoped<Radzen.DialogService>();
            builder.Services.AddScoped<Radzen.NotificationService>();
            builder.Services.AddScoped<Radzen.TooltipService>();
            builder.Services.AddScoped<Radzen.ContextMenuService>();
            builder.Services.AddScoped<Validators.TicketValidator>();
            builder.Services.AddScoped<Validators.TicketInterventionValidator>();
            builder.Services.AddScoped<Validators.TicketEditValidator>();
            builder.Services.AddScoped<SFDialogService>();

            builder.Services.AddLocalization();

            // Build dell'app una sola volta
            var app = builder.Build();

            // Imposta la cultura usando l'app già costruita
            CultureInfo cultureInfo;
            var jsInterop = app.Services.GetRequiredService<IJSRuntime>();
            var appLanguage = await jsInterop.InvokeAsync<string>("appCulture.get");
            if (appLanguage != null && appLanguage != "null")
            {
                cultureInfo = new CultureInfo(appLanguage);
            }
            else
            {
                cultureInfo = new CultureInfo("en-US");
                await jsInterop.InvokeVoidAsync("appCulture.set", "en-US");
            }
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

            var navigationManager = app.Services.GetRequiredService<NavigationManager>();

            HubHelper.HubConnection = new HubConnectionBuilder()
                        .WithUrl(navigationManager.ToAbsoluteUri("/signalRHub"))
                        .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new NotificationJsonConverter()))
                        .WithAutomaticReconnect()                       
                        .Build();

            HubHelper.HubConnection.On<SerializedNotification>("Notification", async (notificationJson) =>
            {
                await DynamicNotificationHandlers.Publish(notificationJson);
            });

            await HubHelper.HubConnection.StartAsync();

            await app.RunAsync();
        }

        public static class DynamicNotificationHandlers
        {
            private static Dictionary<Type, List<(object, Func<SerializedNotification, Task>)>> _handlers = new Dictionary<Type, List<(object, Func<SerializedNotification, Task>)>>();
            public static void Register<T>(INotificationHandler<T> handler) where T : SerializedNotification
            {
                lock (_handlers)
                {
                    var handlerInterfaces = handler
                        .GetType()
                        .GetInterfaces()
                        .Where(x =>
                            x.IsGenericType &&
                            x.GetGenericTypeDefinition() == typeof(INotificationHandler<>))
                        .ToList();
                    foreach (var item in handlerInterfaces)
                    {
                        var notificationType = item.GenericTypeArguments.First();
                        if (!_handlers.TryGetValue(notificationType, out var handlers))
                        {
                            handlers = new List<(object, Func<SerializedNotification, Task>)>();
                            _handlers.Add(notificationType, handlers);
                        }
                        handlers.Add((handler, async s => await handler.Handle((T)s, default(CancellationToken))));
                    }
                }
            }
            public static void Unregister<T>(INotificationHandler<T> handler) where T : SerializedNotification
            {
                lock (_handlers)
                {
                    foreach (var item in _handlers)
                    {
                        item.Value.RemoveAll(h => h.Item1.Equals(handler));
                    }
                }
            }
            public static async Task Publish(SerializedNotification notification)
            {
                try
                {
                    var notificationType = notification.GetType();
                    if (_handlers.TryGetValue(notificationType, out var filtered))
                    {
                        foreach (var item in filtered)
                        {
                            await item.Item2(notification);
                        }
                    }

                }
                catch (System.Exception e)
                {
                    Console.Error.WriteLine(e + " " + e.StackTrace);

                    throw;
                }
            }
        }
    }
}
