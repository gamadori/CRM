using CRM.Client.Services;
using CRM.Server;
using CRM.Server.Controllers;
using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Server.Services;
using CRM.Shared;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using System.Security.Cryptography.X509Certificates;
using static System.Formats.Asn1.AsnWriter;



var builder = WebApplication.CreateBuilder(args);

// Configura licenza QuestPDF (Community - gratuita per progetti sotto $1M revenue)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();


builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                
                .AddDefaultUI()
                .AddTokenProvider<DataProtectorTokenProvider<ApplicationUser>>(TokenOptions.DefaultProvider);



var identityServerBuilder = builder.Services.AddIdentityServer()
    .AddApiAuthorization<ApplicationUser, ApplicationDbContext>(options =>
    {
        options.IdentityResources["openid"].UserClaims.Add("role");
        options.ApiResources.Single().UserClaims.Add("role");
    });
    
var certSubject = builder.Configuration["IdentityServer:Key:Name"];
if (!string.IsNullOrWhiteSpace(certSubject))
{
    using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
    store.Open(OpenFlags.ReadOnly);
    var cert = store.Certificates
        .Find(X509FindType.FindBySubjectDistinguishedName, certSubject, validOnly: false)
        .OfType<X509Certificate2>()
        .FirstOrDefault(c => c.HasPrivateKey);
    if (cert != null)
    {
        identityServerBuilder.AddSigningCredential(cert);
    }
}


builder.Services.AddAuthentication()
    .AddIdentityServerJwt();
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler
    .DefaultInboundClaimTypeMap.Remove("role");


builder.Services.AddLocalApiAuthentication();

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

builder.Services.AddScoped<ITicketsService, TicketsService>();

builder.Services.AddScoped<TranslateService>();

builder.Services.AddScoped<TelegramCommandsService>();

builder.Services.AddSingleton<OpenAIEmbeddingService>();

builder.Services.AddSingleton<OpenAIChatService>();

builder.Services.AddScoped<ILogosService, LogosService>();

builder.Services.AddScoped<ITicketStatesService, TicketStatesService>();



// ✅ AGGIUNTO: Servizio per generare PDF dei ticket
builder.Services.AddScoped<ITicketPdfGenerator, TicketPdfGenerator>();

// ✅ NUOVO: Servizio per generare PDF degli interventi
builder.Services.AddScoped<IInterventionPdfGenerator, InterventionPdfGenerator>();

// ✅ NUOVO: Servizio OTP per verifica firma
builder.Services.AddScoped<ISignatureOtpService, SignatureOtpService>();

// ✅ NUOVO: Servizio per elaborazione scontrini/fatture con Azure Form Recognizer
builder.Services.AddScoped<IReceiptProcessorService, ReceiptProcessorService>();

builder.Services.AddSingleton<WTelegramService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<WTelegramService>());

builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();

// ✅ NUOVO: Servizio per gestione feedback ticket
builder.Services.AddScoped<ITicketFeedbackService, TicketFeedbackService>();

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

var supportedCultures = new[] { "en", "it", "fr", "de", "es" };

builder.Services.Configure<RequestLocalizationOptions>(options => {

    options.SetDefaultCulture(supportedCultures[0])
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
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

var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
    
app.UseRequestLocalization(localizationOptions);


app.UseRouting();

app.UseIdentityServer();
app.UseAuthentication();
app.UseAuthorization();

app.UseCors("RedGPolicy");
app.UseEndpoints(endpoints => {
    app.MapControllers();
});




app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");


app.MapHub<SignalRHub>("/signalRHub");


var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
using (var scope = scopeFactory.CreateScope())
{
    RolesHelper.CreateUserRoles(scope.ServiceProvider).Wait();
}


app.Run();

