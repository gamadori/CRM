using Microsoft.AspNetCore.Hosting;

namespace CRM.Server.Services
{
    public interface IAppOfflineService
    {
        bool Exists();
        Task PublishAsync(CancellationToken cancellationToken = default);
    }

    public sealed class AppOfflineService : IAppOfflineService
    {
        private const string AppOfflineFileName = "app_offline.htm";
        private readonly IWebHostEnvironment _environment;

        public AppOfflineService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public bool Exists() => File.Exists(GetAppOfflinePath());

        public async Task PublishAsync(CancellationToken cancellationToken = default)
        {
            var templatePath = Path.Combine(_environment.ContentRootPath, "Deployment", "app_offline.template.htm");
            var targetPath = GetAppOfflinePath();

            var html = File.Exists(templatePath)
                ? await File.ReadAllTextAsync(templatePath, cancellationToken)
                : GetFallbackHtml();

            await File.WriteAllTextAsync(targetPath, html, cancellationToken);
        }

        private string GetAppOfflinePath() => Path.Combine(_environment.ContentRootPath, AppOfflineFileName);

        private static string GetFallbackHtml() => """
            <!doctype html>
            <html lang="it">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <meta http-equiv="refresh" content="30">
                <title>CRM - Manutenzione</title>
            </head>
            <body>
                <main style="font-family:Arial,sans-serif;text-align:center;margin:12vh auto;max-width:620px;padding:24px">
                    <h1>Manutenzione in corso</h1>
                    <p>Il CRM e' temporaneamente indisponibile per un aggiornamento.</p>
                    <p>La pagina verra' aggiornata automaticamente.</p>
                </main>
            </body>
            </html>
            """;
    }
}
