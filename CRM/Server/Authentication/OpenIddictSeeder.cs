using OpenIddict.Abstractions;

namespace CRM.Server.Authentication
{
    internal static class OpenIddictSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            var configuration = services.GetRequiredService<IConfiguration>();
            var environment = services.GetRequiredService<IHostEnvironment>();
            var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
            var clientUris = configuration.GetSection("OpenIddict:ClientUris").Get<string[]>();
            if (clientUris == null || clientUris.Length == 0)
            {
                if (!environment.IsDevelopment())
                {
                    throw new InvalidOperationException(
                        "Configurare almeno un URL pubblico in OpenIddict:ClientUris.");
                }

                clientUris = ["https://localhost:5001/", "http://localhost:5000/"];
            }

            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = OpenIddictConfiguration.ClientId,
                ClientType = OpenIddictConstants.ClientTypes.Public,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                DisplayName = "CRM Web Client"
            };

            descriptor.Permissions.UnionWith(
            [
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Roles,
                OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConfiguration.ApiScope
            ]);
            descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

            foreach (var clientUri in clientUris.Where(uri => !string.IsNullOrWhiteSpace(uri)))
            {
                var baseUri = new Uri(clientUri, UriKind.Absolute);
                if (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp)
                {
                    throw new InvalidOperationException($"URL client OpenIddict non valido: {baseUri}");
                }
                if (!environment.IsDevelopment() && baseUri.IsLoopback)
                {
                    throw new InvalidOperationException(
                        "OpenIddict:ClientUris non può contenere localhost in produzione.");
                }

                descriptor.RedirectUris.Add(new Uri(baseUri, "authentication/login-callback"));
                descriptor.PostLogoutRedirectUris.Add(new Uri(baseUri, "authentication/logout-callback"));
            }

            var application = await manager.FindByClientIdAsync(OpenIddictConfiguration.ClientId);
            if (application == null)
            {
                await manager.CreateAsync(descriptor);
            }
            else
            {
                await manager.UpdateAsync(application, descriptor);
            }
        }
    }
}
