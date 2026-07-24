using CRM.Client.Services;
using CRM.Server;
using CRM.Server.Authentication;
using CRM.Server.Controllers;
using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;



var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configura licenza QuestPDF (Community - gratuita per progetti sotto $1M revenue)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.UseOpenIddict();
    // Le migration sono gestite a mano (il tool 'dotnet ef migrations add' e' incompatibile con
    // questo SDK .NET 10, vedi memoria ef-tooling): lo snapshot puo' non essere perfettamente
    // allineato, quindi si sopprime il blocco su modifiche pendenti in fase di database update.
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Aggiungi IHttpContextAccessor per accedere all'HttpContext nei servizi
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<MaintenanceState>();
builder.Services.AddSingleton<IAppOfflineService, AppOfflineService>();
builder.Services.AddHostedService<MaintenanceAppOfflineBackgroundService>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                
                .AddDefaultUI()
                .AddTokenProvider<DataProtectorTokenProvider<ApplicationUser>>(TokenOptions.DefaultProvider);



builder.Services.AddCrmAuthentication(builder.Configuration, builder.Environment);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddScoped<ILogEventService, LogEventService>();

builder.Services.AddScoped<IPermitsService, PermitsService>();
builder.Services.AddScoped<IEmailSender, EmailService>();
builder.Services.AddScoped<IEmailSenderPlus, EmailService>();
//builder.Services.AddSingleton<IAPIEmailSender>(sp =>
//    new SendGridEmailSender(
//        builder.Configuration["Email:SendGridKey"]!,
//        builder.Configuration["Email:From"]!
//    ));

//builder.Services.AddSingleton<IAPIEmailSender>(sp =>
//    new BrevoEmailSender(
//        sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
//        builder.Configuration["Email:BrevoKey"]!,
//        builder.Configuration["Email:From"]!
//    ));

builder.Services.AddTransient<INoticeService, NoticeService>();

builder.Services.AddScoped<IEmailBuilderService, EmailBuilderService>();

builder.Services.AddScoped<IArchiveService, ArchiveService>();

builder.Services.AddScoped<IDocxToPdfConverter, LibreOfficeDocxToPdfConverter>();

builder.Services.AddScoped<ILanguagesService, LanguagesService>();

builder.Services.AddScoped<SignInManager<ApplicationUser>, ApplicationSignInManager<ApplicationUser>>();

builder.Services.AddScoped<ILangSelectorService, LangSelectorService>();

builder.Services.AddScoped<CRM.Server.Services.ITicketsService, TicketsService>();
builder.Services.AddScoped<ITicketSummaryService, TicketSummaryService>();
builder.Services.AddScoped<ITicketNotificationService, TicketNotificationService>();

builder.Services.AddScoped<TranslateService>();

builder.Services.AddScoped<TelegramCommandsService>();

builder.Services.AddSingleton<OpenAIEmbeddingService>();

builder.Services.AddSingleton<OpenAIChatService>();

builder.Services.AddScoped<IInterventionsService, InterventionsService>();

builder.Services.AddScoped<IKnowledgeService, KnowledgeService>();

builder.Services.AddScoped<TicketKnowledgeService>();

builder.Services.AddScoped<CrmAssistantService>();

builder.Services.AddSingleton<IVoiceTranscriptionService, VoiceTranscriptionService>();

builder.Services.AddScoped<ILogosService, LogosService>();

builder.Services.AddScoped<ITicketStatesService, TicketStatesService>();

builder.Services.AddScoped<IInterventionTypesService, InterventionTypesService>();

builder.Services.AddScoped<ITicketPdfGenerator, TicketPdfGenerator>();

builder.Services.AddScoped<IInterventionPdfGenerator, InterventionPdfGenerator>();

// Singleton: la chiave HMAC deve restare stabile tra la generazione e la verifica
// dell'OTP (con Scoped, senza Security:OtpSecret, ogni richiesta rigenerava una
// chiave casuale e la verifica falliva sempre).
builder.Services.AddSingleton<ISignatureOtpService, SignatureOtpService>();

// --- Invio OTP via SMS (provider-neutrale) ---
builder.Services.Configure<CRM.Server.Services.Sms.SmsOptions>(
    builder.Configuration.GetSection(CRM.Server.Services.Sms.SmsOptions.SectionName));

if (string.Equals(builder.Configuration["Sms:Provider"], "Twilio", System.StringComparison.OrdinalIgnoreCase))
    builder.Services.AddHttpClient<CRM.Server.Services.Sms.ISmsSender, CRM.Server.Services.Sms.TwilioSmsSender>();
else
    builder.Services.AddSingleton<CRM.Server.Services.Sms.ISmsSender, CRM.Server.Services.Sms.NullSmsSender>();
builder.Services.AddScoped<IAttachmentsService, AttachmentsService>();
builder.Services.AddScoped<CRM.Server.Services.IProductCatalogAssetsService, ProductCatalogAssetsService>();
builder.Services.AddScoped<CRM.Server.Services.IProductCatalogService, ProductCatalogService>();

builder.Services.AddScoped<IReceiptProcessorService, ReceiptProcessorService>();

builder.Services.AddScoped<IExpenseReceiptService, ExpenseReceiptService>();

builder.Services.AddSingleton<WTelegramService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<WTelegramService>());

builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();

builder.Services.AddScoped<ICompaniesService, CompaniesService>();

builder.Services.AddScoped<IProductsService, ProductsService>();
builder.Services.AddScoped<IMachineParameterApiKeyService, MachineParameterApiKeyService>();
builder.Services.AddScoped<IExternalTicketApiService, ExternalTicketApiService>();
builder.Services.AddScoped<CRM.Server.Services.IMachineBackupsService, MachineBackupsService>();

builder.Services.AddScoped<IProductTypesService, ProductTypesService>();

builder.Services.AddScoped<ITicketFeedbackService, TicketFeedbackService>();

builder.Services.AddScoped<IFoldersService, FoldersService>();

builder.Services.AddScoped<IContactsService, ContactsService>();

builder.Services.AddScoped<IDealsService, DealsService>();

builder.Services.AddScoped<ILeadsService, LeadsService>();

builder.Services.AddScoped<IWorkflowAutomationService, WorkflowAutomationService>();

builder.Services.AddScoped<IQuotesService, QuotesService>();

builder.Services.AddScoped<IQuotePdfGenerator, QuotePdfGenerator>();

builder.Services.AddScoped<IOrdersService, OrdersService>();

builder.Services.AddScoped<IOrderPdfGenerator, OrderPdfGenerator>();

builder.Services.AddScoped<ICommesseService, CommesseService>();
builder.Services.AddScoped<ICommessaFasiService, CommessaFasiService>();
builder.Services.AddScoped<IGanttPlansService, GanttPlansService>();
builder.Services.AddScoped<IGanttPhasesService, GanttPhasesService>();

builder.Services.AddScoped<CRM.Server.Services.IPriceListService, CRM.Server.Services.PriceListService>();

builder.Services.AddScoped<IInvoicesService, InvoicesService>();

builder.Services.AddScoped<IActivitiesService, ActivitiesService>();
builder.Services.AddScoped<CRM.Server.Services.ICalendarService, CRM.Server.Services.CalendarService>();

// IHttpClientFactory per i canali email basati su API (es. Brevo) e altri client HTTP.
builder.Services.AddHttpClient();
builder.Services.AddScoped<CRM.Server.Services.Email.IEmailEngagementService, CRM.Server.Services.Email.EmailEngagementService>();
builder.Services.AddScoped<CRM.Server.Services.Email.IInboundEmailAiService, CRM.Server.Services.Email.InboundEmailAiService>();
builder.Services.AddScoped<CRM.Server.Services.Email.IEmailTemplateTranslator, CRM.Server.Services.Email.EmailTemplateTranslator>();
builder.Services.AddScoped<CRM.Server.Services.Email.IInboundEmailRouter, CRM.Server.Services.Email.InboundEmailRouter>();
builder.Services.AddHostedService<ReminderBackgroundService>();
builder.Services.AddHostedService<TicketReminderBackgroundService>();
builder.Services.AddHostedService<WorkflowAutomationBackgroundService>();
builder.Services.AddHostedService<EmailOutboxBackgroundService>();
builder.Services.AddHostedService<EmailInboxBackgroundService>();

// Provider di fatturazione elettronica: di default nessuno (genera XML ma non trasmette).
// Sostituire con un adapter specifico del provider adottato (Aruba, InfoCert, TeamSystem, ...).
builder.Services.AddScoped<IEInvoiceProvider, NullEInvoiceProvider>();

builder.Services.AddScoped<IInterventionTypeLangsService, InterventionTypeLangsService>();

builder.Services.AddScoped<IFolderLanguagesService, FolderLanguagesService>();

builder.Services.AddScoped<IArticlesService, ArticlesService>();

builder.Services.AddSingleton<IRsaLicenseService, RsaLicenseService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "RedGPolicy",
                      builder =>
                      {
                          builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                      });
});

builder.Services.AddAuthorization(options =>
{
    foreach (var r in Enum.GetValues(typeof(ePolicy)))
    {
        if (r != null)
        {
            options.AddPolicy(r.ToString() ?? ((int)r).ToString(),
                policy => policy.RequireRole(PolicyRoles.vPoliyRoles[(int)r]));
        }
    }

    
});

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[] { "en", "it", "fr", "de", "es" };

builder.Services.Configure<RequestLocalizationOptions>(options => {

    options.SetDefaultCulture(supportedCultures[0])
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
    // Priorità: Accept-Language header prima del cookie
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new AcceptLanguageHeaderRequestCultureProvider(),
        new CookieRequestCultureProvider(),
        new QueryStringRequestCultureProvider()
    };
});

builder.Services.AddControllersWithViews()
    .AddViewLocalization
    (LanguageViewLocationExpanderFormat.SubFolder)
    .AddDataAnnotationsLocalization();

builder.Services.AddControllers();

builder.Services.AddRazorPages();

builder.Services.AddSignalR();
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});



var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// Seed database (roles, default data, admin user)
using (var scopeSeed = app.Services.CreateScope())
{
    // run seeding synchronously to avoid changing method signature
   // CRM.Server.Data.DbInitializer.SeedAsync(scopeSeed.ServiceProvider).GetAwaiter().GetResult();
}

app.UseResponseCompression();



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseWebAssemblyDebugging();
   
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// Culture from the HttpRequest

//var localizationOptions = new RequestLocalizationOptions()
//    .SetDefaultCulture(supportedCultures[0])
//    .AddSupportedCultures(supportedCultures)
//    .AddSupportedUICultures(supportedCultures);
    
app.UseRequestLocalization();


app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseCors("RedGPolicy");

app.MapRazorPages();

// SICUREZZA: tutta l'API richiede un utente autenticato per DEFAULT. Prima questa riga era
// un semplice MapControllers() e la protezione dipendeva dall'attributo [Authorize] scritto a
// mano su ogni controller: dei 92 controller 48 ne erano sprovvisti, quindi endpoint come
// /api/Quotes/list o /api/SmtpSettings rispondevano a chiunque, senza login.
// Gli endpoint che devono restare pubblici perché si autenticano con un token proprio
// (webhook email, API esterne con X-Api-Key, licenze macchina, conferma firma via link email)
// sono marcati [AllowAnonymous], che ha la precedenza su questa regola.
// Nota: vale solo per i controller — Razor Pages (login Identity), index.html del client WASM
// e l'hub SignalR restano com'erano.
app.MapControllers().RequireAuthorization();

app.MapFallbackToFile("index.html");


app.MapHub<SignalRHub>("/signalRHub");


var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
using (var scope = scopeFactory.CreateScope())
{
    RolesHelper.CreateUserRoles(scope.ServiceProvider).Wait();
    CRM.Server.Authentication.OpenIddictSeeder.SeedAsync(scope.ServiceProvider).GetAwaiter().GetResult();
}


app.Run();

