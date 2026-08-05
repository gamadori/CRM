using CRM.Mobile.Pages;
using CRM.Mobile.Services;

namespace CRM.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseMauiApp<App>();

        builder.Services.AddSingleton<AppSettingsStore>();
        builder.Services.AddSingleton<CrmApiClient>();
        builder.Services.AddSingleton<LeadQueueStore>();
        builder.Services.AddSingleton<InitiativeCatalog>();

        // Singleton: si iscrive al cambio di connettivita' e deve restare vivo anche quando
        // nessuna pagina e' aperta, altrimenti il ritorno della rete non fa ripartire la coda.
        builder.Services.AddSingleton<LeadSyncService>();

        builder.Services.AddSingleton<CapturePage>();
        builder.Services.AddSingleton<SettingsPage>();

        return builder.Build();
    }
}
