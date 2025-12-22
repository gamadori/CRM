using CRM.Server.Data;
using CRM.Shared;
using CRM.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System.Threading.Tasks;
using System;
using CNM.Authorize;
using CNM.Helpers;
using Syncfusion.Licensing;



namespace CRM.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    Configuration.GetConnectionString("DefaultConnection")));

            services.AddDatabaseDeveloperPageExceptionFilter();

            //services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
            //    .AddEntityFrameworkStores<ApplicationDbContext>()
            //    .AddTokenProvider<DataProtectorTokenProvider<ApplicationUser>>("InsertUser") ;

            services.AddIdentity<ApplicationUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultUI()
                .AddTokenProvider<DataProtectorTokenProvider<ApplicationUser>>(TokenOptions.DefaultProvider);

            services.AddIdentityServer()
               .AddApiAuthorization<ApplicationUser, ApplicationDbContext>(options => {
                   options.IdentityResources["openid"].UserClaims.Add("role");
                   options.ApiResources.Single().UserClaims.Add("role");
               });

            //services.AddIdentityServer()
            //    .AddApiAuthorization<ApplicationUser, ApplicationDbContext>();
            services.AddMemoryCache();

            //SyncfusionLicenseProvider.RegisterLicense("NTE2MjAzQDMxMzkyZTMyMmUzMFQzVFRJaW8zTWwrNVdzS2ovWUVKY0NPZWNPQkVoYUlMZVNXNy8vZ0hNZU09");
            SyncfusionLicenseProvider.RegisterLicense("NTIwMjM3QDMxMzkyZTMzMmUzMEdDZ1laU2VLb0VTUGk4SnVYSjJ4b2kwUG5UYnR6RmlQdkJqNU43dG1lVUE9");
            services.AddHttpContextAccessor();

            services.AddAuthentication()
                .AddIdentityServerJwt();

            System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler
                .DefaultInboundClaimTypeMap.Remove("role");

            services.AddScoped<IPermitsService, PermitsService>();
            services.AddScoped<IEmailSender, EmailService>();
            services.AddTransient<IEmailSenderPlus, EmailService>();

            services.AddScoped<IEmailBuilderService, EmailBuilderService>();
            services.AddScoped<IEmailSenderPlus, EmailService>();
            services.AddScoped<IArchiveService, ArchiveService>();


            services.AddScoped<SignInManager<ApplicationUser>, ApplicationSignInManager<ApplicationUser>>();

           

            services.AddCors(options =>
            {
                options.AddPolicy(name: "MyAllowSpecificOrigins",
                                  builder =>
                                  {
                                      builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                                  });
            });

            services.AddAuthorization(options =>
            {
                foreach (var r in Enum.GetValues(typeof(ePolicy)))
                {
                    options
                    .AddPolicy(r.ToString(),
                        policy => policy.RequireRole(PolicyRoles.vPoliyRoles[(int)r]));
                }
            });
            //services.AddControllersWithViews().AddNewtonsoftJson(options =>
            //{
            //    options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver();
            //});
            services.AddControllersWithViews();
            services.AddRazorPages();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
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
            
            app.UseRouting();

            app.UseIdentityServer();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
                endpoints.MapFallbackToFile("index.html");
            });

            app.UseCors("MyAllowSpecificOrigins");
            
            CreateUserRoles(serviceProvider).Wait();
        }

        private async Task CreateUserRoles(IServiceProvider serviceProvider)
        {
            var RoleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var UserManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            IdentityResult roleResult;

            foreach (eRoles role in Enum.GetValues(typeof(eRoles)))
            {
                var r = role.ToString();
                var roleCheck = await RoleManager.RoleExistsAsync(r);
                if (!roleCheck)
                {
                    //create the roles and seed them to the database
                    roleResult = await RoleManager.CreateAsync(new IdentityRole(r));
                }
            }


        }
    }
}
