using CRM.Server.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using System.Security.Cryptography.X509Certificates;

namespace CRM.Server.Authentication
{
    internal static class AuthenticationServiceCollectionExtensions
    {
        public static IServiceCollection AddCrmAuthentication(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            var certificate = FindCertificate(configuration["OpenIddict:CertificateSubject"]);

            // Serve alla pagina di login esterna per conoscere la politica di aggancio del provider.
            services.Configure<ExternalAuthenticationOptions>(
                configuration.GetSection(ExternalAuthenticationOptions.SectionName));

            services.AddOpenIddict()
                .AddCore(options =>
                {
                    options.UseEntityFrameworkCore()
                        .UseDbContext<ApplicationDbContext>();
                })
                .AddServer(options =>
                {
                    options.SetAuthorizationEndpointUris("connect/authorize")
                        .SetTokenEndpointUris("connect/token")
                        .SetUserInfoEndpointUris("connect/userinfo")
                        .SetEndSessionEndpointUris("connect/logout");

                    options.AllowAuthorizationCodeFlow()
                        .AllowRefreshTokenFlow()
                        .RequireProofKeyForCodeExchange();

                    options.RegisterScopes(
                        OpenIddictConstants.Scopes.Email,
                        OpenIddictConstants.Scopes.Profile,
                        OpenIddictConstants.Scopes.Roles,
                        OpenIddictConfiguration.ApiScope);

                    if (certificate != null)
                    {
                        options.AddSigningCertificate(certificate)
                            .AddEncryptionCertificate(certificate);
                    }
                    else if (environment.IsDevelopment())
                    {
                        options.AddDevelopmentSigningCertificate()
                            .AddDevelopmentEncryptionCertificate();
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "OpenIddict:CertificateSubject deve indicare un certificato con chiave privata in produzione.");
                    }

                    options.UseAspNetCore()
                        .EnableAuthorizationEndpointPassthrough()
                        .EnableTokenEndpointPassthrough()
                        .EnableUserInfoEndpointPassthrough()
                        .EnableEndSessionEndpointPassthrough()
                        .EnableStatusCodePagesIntegration();
                })
                .AddValidation(options =>
                {
                    options.UseLocalServer();
                    options.UseAspNetCore();
                });

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = OpenIddictConfiguration.AuthenticationScheme;
                options.DefaultAuthenticateScheme = OpenIddictConfiguration.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIddictConfiguration.AuthenticationScheme;
            })
            .AddPolicyScheme(
                OpenIddictConfiguration.AuthenticationScheme,
                null,
                options => options.ForwardDefaultSelector = SelectAuthenticationScheme)
            // Provider di login esterni da configurazione: se non ne è abilitato nessuno,
            // non viene registrato alcuno schema e il login resta quello locale.
            .AddExternalProviders(configuration);

            return services;
        }

        private static string SelectAuthenticationScheme(HttpContext context)
        {
            var usesBearerToken = context.Request.Headers.Authorization.ToString()
                .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
            var isApiRequest = context.Request.Path.StartsWithSegments("/api")
                || context.Request.Path.StartsWithSegments("/localApi");

            return usesBearerToken || isApiRequest
                ? OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme
                : IdentityConstants.ApplicationScheme;
        }

        private static X509Certificate2? FindCertificate(string? subject)
        {
            if (string.IsNullOrWhiteSpace(subject))
            {
                return null;
            }

            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            return store.Certificates
                .Find(X509FindType.FindBySubjectDistinguishedName, subject, validOnly: false)
                .OfType<X509Certificate2>()
                .FirstOrDefault(item => item.HasPrivateKey);
        }
    }
}
