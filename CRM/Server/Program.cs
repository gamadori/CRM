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
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using QuestPDF.Infrastructure;



var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configura licenza QuestPDF (Community - gratuita per progetti sotto $1M revenue)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// ─── Chiavi di cifratura ────────────────────────────────────────────────────────────────
// Le password della posta e le chiavi dei provider sono cifrate sul database (vedi
// ISecretProtector). Le chiavi che le decifrano stanno in questa cartella: se sparisce, quei
// segreti non sono piu' leggibili e vanno reinseriti a mano. VA NEL BACKUP.
//
// Il percorso e' obbligatorio di proposito. Lasciandolo vuoto, Data Protection userebbe una
// posizione di default che in certi scenari (IIS senza profilo utente caricato) viene ricreata
// a ogni riavvio: si cifrerebbe con chiavi che spariscono, cioe' il modo peggiore di perdere
// dei dati - in silenzio e solo dopo un riavvio, quando ormai nessuno collega le due cose.
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    throw new InvalidOperationException(
        "DataProtection:KeysPath non è configurato. Serve una cartella dove conservare le chiavi " +
        "che cifrano i segreti sul database (password SMTP/IMAP, chiavi dei provider). " +
        "Impostala in appsettings.json, per esempio \"DataProtection\": { \"KeysPath\": " +
        "\"C:\\\\ProgramData\\\\CRM\\\\DataProtection-Keys\" }, su un percorso incluso nei backup " +
        "e non accessibile agli utenti dell'applicazione. Perdere quella cartella significa " +
        "reinserire a mano le credenziali della posta.");
}

dataProtectionKeysPath = Environment.ExpandEnvironmentVariables(dataProtectionKeysPath);
Directory.CreateDirectory(dataProtectionKeysPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    // Senza un nome esplicito lo scopo include il percorso dell'applicazione: spostare
    // l'installazione in un'altra cartella renderebbe illeggibile tutto il pregresso.
    .SetApplicationName("CRM");

builder.Services.AddSingleton<CRM.Server.Data.ISecretProtector, CRM.Server.Data.DataProtectionSecretProtector>();

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.UseOpenIddict();
    // Il modello cambia a seconda che i segreti siano cifrati o no: la cache deve saperlo.
    options.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory,
                           CRM.Server.Data.SecretAwareModelCacheKeyFactory>();
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

// Registro dei consumi dei servizi esterni a pagamento. Singleton con scope proprio: viene
// chiamato anche da servizi in background, e non deve mai scrivere sul DbContext del chiamante.
builder.Services.Configure<CRM.Server.Services.Usage.AiPricingOptions>(
    builder.Configuration.GetSection(CRM.Server.Services.Usage.AiPricingOptions.SectionName));
builder.Services.AddSingleton<CRM.Server.Services.Usage.IUsageRecorder, CRM.Server.Services.Usage.UsageRecorder>();

builder.Services.AddScoped<CRM.Server.Services.ITicketsService, TicketsService>();
builder.Services.AddScoped<ITicketSummaryService, TicketSummaryService>();
builder.Services.AddScoped<ITicketNotificationService, TicketNotificationService>();
builder.Services.AddScoped<ITicketChatNotificationService, TicketChatNotificationService>();
builder.Services.AddScoped<ITicketReminderNotificationService, TicketReminderNotificationService>();
builder.Services.AddScoped<ITicketBlockNotificationService, TicketBlockNotificationService>();

// Smistamento automatico dei ticket verso i gruppi: il client AI e' separato dalle regole,
// cosi' soglia, candidati e ripiego restano verificabili senza chiamare il modello.
builder.Services.AddScoped<CRM.Server.Services.TicketRouting.ITicketRoutingAiClient, CRM.Server.Services.TicketRouting.TicketRoutingAiClient>();
builder.Services.AddScoped<CRM.Server.Services.TicketRouting.ITicketRoutingService, CRM.Server.Services.TicketRouting.TicketRoutingService>();

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

builder.Services.AddScoped<IReceiptAnalyzer, AzureReceiptAnalyzer>();
builder.Services.AddScoped<IReceiptProcessorService, ReceiptProcessorService>();
builder.Services.AddScoped<IBusinessCardAnalyzer, AzureBusinessCardAnalyzer>();

// Tipologia della nota spese in tre livelli: sottotipo del documento, dizionario di esercenti
// e - solo se i primi due tacciono e qualcuno l'ha acceso - il modello. Come per lo smistamento
// dei ticket, il client AI e' separato dalle regole, cosi' queste restano verificabili senza rete.
builder.Services.AddScoped<CRM.Server.Services.ExpenseCategorization.IExpenseCategoryAiClient, CRM.Server.Services.ExpenseCategorization.ExpenseCategoryAiClient>();
builder.Services.AddScoped<CRM.Server.Services.ExpenseCategorization.IExpenseCategorizer, CRM.Server.Services.ExpenseCategorization.ExpenseCategorizer>();

// Chiavi API di ogni ambito (backup macchina, ticket esterni, app fiera): un punto solo di
// generazione e verifica, con l'ambito che impedisce a una chiave di valere fuori dal suo uso.
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();

// Ponte con l'app di cattura biglietti in fiera (autenticazione a chiave, non OIDC).
builder.Services.AddScoped<IFieldApiService, FieldApiService>();

builder.Services.AddScoped<IExpenseReceiptService, ExpenseReceiptService>();

// Prospetto PDF delle note spese per tipologia: i conti li fa il servizio, qui c'e' solo la resa.
builder.Services.AddScoped<IExpenseReportPdfGenerator, ExpenseReportPdfGenerator>();

// Cambi BCE per le spese sostenute all'estero. HttpClient tipizzato: il servizio ne configura
// il timeout, cosi' un cambio lento non rallenta il salvataggio di una nota spese.
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IExchangeRateService, ExchangeRateService>();

builder.Services.AddSingleton<WTelegramService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<WTelegramService>());

builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();

builder.Services.AddScoped<ICompaniesService, CompaniesService>();

builder.Services.AddScoped<IProductsService, ProductsService>();
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
builder.Services.AddScoped<IInitiativesService, InitiativesService>();
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

    // Le migration esistono solo su un database vero: la suite di prova monta l'applicazione
    // intera su un provider in memoria, dove Migrate() non ha significato e fallisce.
    if (db.Database.IsRelational())
        db.Database.Migrate();
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

// In sviluppo il rimbalzo su HTTPS resta spento, cosi' un telefono in LAN puo' aprire il CRM su
// http://<ip>:5000 senza inciampare nel certificato, che e' emesso per "localhost" e su un
// indirizzo di rete darebbe nome sbagliato e CA sconosciuta.
//
// Cosa si perde e cosa no: su HTTP il service worker non si registra (niente PWA ne' shell
// offline), mentre continuano a funzionare l'attributo "capture" della cattura biglietti - e' il
// selettore file nativo, non getUserMedia - e IndexedDB, quindi anche la coda offline dei lead.
// Per provare la PWA da telefono serve invece un certificato valido per l'IP (mkcert).
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

// Nessun file servito da UseStaticFiles ha l'hash nel nome (index.html, il bundle degli scoped
// CSS CRM.Client.styles.css, css/*.css, il service worker): senza Cache-Control il browser calcola
// una freschezza euristica dal Last-Modified, considera valida la copia in cache e la richiesta non
// arriva nemmeno al server. Risultato: modifiche a un .razor.css invisibili anche dopo aver
// svuotato le cache del service worker, che e' network-first e quindi innocente — il suo fetch
// passa comunque dalla cache HTTP.
//
// "no-cache" NON disattiva la cache: impone la rivalidazione. Con l'ETag gia' presente la risposta
// e' un 304 da poche centinaia di byte. index.html e' il file per cui conta di piu', perche'
// contiene i ?nocache=N degli altri asset: se e' stantio, nessun altro aggiornamento propaga.
//
// _framework/* non passa da qui: UseBlazorFrameworkFiles() gli mette gia' no-cache da solo.
var revalidateAlways = new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
};

app.UseBlazorFrameworkFiles();
app.UseStaticFiles(revalidateAlways);

if (app.Environment.IsDevelopment())
{
    var targetFramework = $"net{Environment.Version.Major}.0";
    var clientFrameworkPath = new[]
    {
        Path.Combine(app.Environment.ContentRootPath, "..", "Client", "bin", "Debug", targetFramework, "wwwroot", "_framework"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Client", "bin", "Debug", targetFramework, "wwwroot", "_framework"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "Client", "bin", "Debug", targetFramework, "wwwroot", "_framework")
    }
    .Select(Path.GetFullPath)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .FirstOrDefault(Directory.Exists);

    if (!string.IsNullOrWhiteSpace(clientFrameworkPath))
    {
        var frameworkContentTypes = new FileExtensionContentTypeProvider();
        frameworkContentTypes.Mappings[".wasm"] = "application/wasm";
        frameworkContentTypes.Mappings[".pdb"] = "application/octet-stream";
        frameworkContentTypes.Mappings[".dat"] = "application/octet-stream";

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(clientFrameworkPath),
            RequestPath = "/_framework",
            ContentTypeProvider = frameworkContentTypes,
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream",
            // In sviluppo questi file cambiano a ogni build: senza rivalidazione si ricade
            // nelle rogne di asset Blazor stantii.
            OnPrepareResponse = revalidateAlways.OnPrepareResponse
        });
    }
}

// Qui c'era app.MapStaticAssets(), rimossa perche' non veniva MAI raggiunta: mappa endpoint, e gli
// endpoint sono valutati dal routing (piu' sotto), mentre UseStaticFiles e' middleware e serve il
// file prima. Restava una riga che sembrava governare la cache degli asset senza governare niente.
//
// Non e' stata "riparata" spostando la pipeline perche' i suoi due vantaggi qui non si incassano:
//  - le varianti precompresse: la compressione e' gia' attiva con UseResponseCompression (sopra),
//    quindi i byte sul filo sono gia' compressi e si risparmierebbe solo CPU;
//  - le URL con fingerprint e cache immutable: richiedono @Assets["..."] da una pagina Razor, e
//    qui index.html e' un file statico servito com'e', quindi non e' possibile referenziarle.
// Con no-cache + ETag la correttezza e' garantita e il costo e' un 304 vuoto per file.

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

// index.html arriva da qui, non da UseStaticFiles: e' un endpoint, quindi le opzioni vanno passate
// anche a questa chiamata perche' riceva il no-cache.
app.MapFallbackToFile("index.html", revalidateAlways);


app.MapHub<SignalRHub>("/signalRHub");


var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
using (var scope = scopeFactory.CreateScope())
{
    RolesHelper.CreateUserRoles(scope.ServiceProvider).Wait();
    CRM.Server.Authentication.OpenIddictSeeder.SeedAsync(scope.ServiceProvider).GetAwaiter().GetResult();

    // Dati minimi senza i quali un database appena creato non e' utilizzabile (lingue, stati
    // ticket, impostazioni). Additivo e idempotente: gira anche in produzione senza toccare
    // nulla di gia' presente. Vedi DbInitializer.
    CRM.Server.Data.DbInitializer.SeedAsync(scope.ServiceProvider).GetAwaiter().GetResult();

    // Porta sotto cifratura i segreti scritti prima che la cifratura esistesse, e segnala quelli
    // che le chiavi attuali non riescono piu' a leggere.
    CRM.Server.Data.SecretsProtectionStartup.RunAsync(scope.ServiceProvider).GetAwaiter().GetResult();
}


app.Run();

/// <summary>
/// Reso visibile perche' la suite di prova monta l'applicazione intera (WebApplicationFactory) per
/// verificare chi puo' chiamare cosa: e' l'unico modo di provare l'autorizzazione com'e' davvero,
/// pipeline e attributi compresi, invece di rileggere gli attributi con la riflessione.
/// </summary>
public partial class Program { }
