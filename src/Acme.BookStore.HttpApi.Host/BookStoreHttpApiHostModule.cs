using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Acme.BookStore.EntityFrameworkCore;
using Acme.BookStore.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic;
using Microsoft.OpenApi.Models;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Authentication.JwtBearer;
using Volo.Abp.AspNetCore.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Acme.BookStore;

[DependsOn(
    typeof(BookStoreHttpApiModule),
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMultiTenancyModule),
    typeof(BookStoreApplicationModule),
    typeof(BookStoreEntityFrameworkCoreModule),
    typeof(AbpAspNetCoreMvcUiBasicThemeModule),
    typeof(AbpAspNetCoreAuthenticationJwtBearerModule),
    typeof(AbpAccountWebIdentityServerModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpSwashbuckleModule)
)]
public class BookStoreHttpApiHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        ConfigureBundles();
        ConfigureUrls(configuration);
        ConfigureConventionalControllers();
        ConfigureAuthentication(context, configuration);
        ConfigureLocalization();
        ConfigureVirtualFileSystem(context);
        ConfigureCors(context, configuration);
        ConfigureSwaggerServices(context, configuration);
    }

    private void ConfigureBundles()
    {
        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(
                BasicThemeBundles.Styles.Global,
                bundle => { bundle.AddFiles("/global-styles.css"); }
            );
        });
    }

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
            options.RedirectAllowedUrls.AddRange(configuration["App:RedirectAllowedUrls"].Split(','));

            options.Applications["Angular"].RootUrl = configuration["App:ClientUrl"];
            options.Applications["Angular"].Urls[AccountUrlNames.PasswordReset] = "account/reset-password";
        });
    }

    private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        if (hostingEnvironment.IsDevelopment())
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.ReplaceEmbeddedByPhysical<BookStoreDomainSharedModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        $"..{Path.DirectorySeparatorChar}Acme.BookStore.Domain.Shared"));
                options.FileSets.ReplaceEmbeddedByPhysical<BookStoreDomainModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        $"..{Path.DirectorySeparatorChar}Acme.BookStore.Domain"));
                options.FileSets.ReplaceEmbeddedByPhysical<BookStoreApplicationContractsModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        $"..{Path.DirectorySeparatorChar}Acme.BookStore.Application.Contracts"));
                options.FileSets.ReplaceEmbeddedByPhysical<BookStoreApplicationModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        $"..{Path.DirectorySeparatorChar}Acme.BookStore.Application"));
            });
        }
    }

    private void ConfigureConventionalControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(BookStoreApplicationModule).Assembly);
        });
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context, IConfiguration configuration)
    {
        var authBuilder = context.Services.AddAuthentication()
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["AuthServer:Authority"];
                options.RequireHttpsMetadata = Convert.ToBoolean(configuration["AuthServer:RequireHttpsMetadata"]);
                options.Audience = "BookStore";
                options.BackchannelHttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
            });

        var azureAdTenantId = configuration["AzureAd1:TenantId"];
        if (azureAdTenantId != null)
        {

            authBuilder.AddOpenIdConnect("AzureOpenIdConnect1", "AzureOpenIdConnect1", options =>
            {
                options.Authority = "https://login.microsoftonline.com/" + azureAdTenantId + "/v2.0/";
                options.ClientId = configuration["AzureAd1:ClientId"];
                options.ResponseType = OpenIdConnectResponseType.CodeIdToken;
                options.CallbackPath = configuration["AzureAd1:CallbackPath"];
                options.ClientSecret = configuration["AzureAd1:ClientSecret"];
                options.RequireHttpsMetadata = false;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.Scope.Add("email");
                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");

                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    // Here you can add custom logic before redirecting to the identity provider
                    // For example, you can log the redirection or modify the request
                    var some = context.Response;
                    return Task.CompletedTask;
                };

                options.Events.OnUserInformationReceived = context =>
                {

                    var some = context.Response;
                    return Task.CompletedTask;
                };

                options.Events.OnRemoteFailure = context =>
                {
                    if (context.Scheme.Name == "AzureOpenIdConnect1")
                    {
                        // Your custom logic for handling remote failure for AzureOpenIdConnect1
                        var some = context.Response;

                    
                    }

                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToIdentityProvider = context =>
                {

                    var some = context.Response;
                    return Task.CompletedTask;
                };


            });
        }
        //Test-SSO
        var azureAdTenantId2 = configuration["AzureAd2:TenantId"];
        if (azureAdTenantId2 != null)
        {

            authBuilder.AddOpenIdConnect("AzureOpenIdConnect2", "AzureOpenIdConnect2", options =>
            {
                options.Authority = "https://login.microsoftonline.com/" + azureAdTenantId2 + "/v2.0/";
                options.ClientId = configuration["AzureAd2:ClientId"];
                options.ResponseType = OpenIdConnectResponseType.CodeIdToken;
                options.CallbackPath = "/signin-azuread-oidc";
                options.ClientSecret = configuration["AzureAd2:ClientSecret"];
                options.RequireHttpsMetadata = false;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.Scope.Add("email");
                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");

                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    // Here you can add custom logic before redirecting to the identity provider
                    // For example, you can log the redirection or modify the request
                    var some = context.Response;
                    return Task.CompletedTask;
                };

                options.Events.OnUserInformationReceived = context =>
                {

                    var some = context.Response;
                    return Task.CompletedTask;
                };

                options.Events.OnRemoteFailure = context =>
                {

                    var some = context.Response;
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToIdentityProvider = context =>
                {
                    if (context.Scheme.Name == "AzureOpenIdConnect2")
                    {
                        // Your custom logic for handling remote failure for AzureOpenIdConnect1
                        var some = context.Response;
                    }
                    return Task.CompletedTask;
                };

            });
        }
    }

    private static void ConfigureSwaggerServices(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAbpSwaggerGenWithOAuth(
            configuration["AuthServer:Authority"],
            new Dictionary<string, string>
            {
                    {"BookStore", "BookStore API"}
            },
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "BookStore API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
            });
    }

    private void ConfigureLocalization()
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Languages.Add(new LanguageInfo("ar", "ar", "العربية"));
            options.Languages.Add(new LanguageInfo("cs", "cs", "Čeština"));
            options.Languages.Add(new LanguageInfo("en", "en", "English"));
            options.Languages.Add(new LanguageInfo("en-GB", "en-GB", "English (UK)"));
            options.Languages.Add(new LanguageInfo("fi", "fi", "Finnish"));
            options.Languages.Add(new LanguageInfo("fr", "fr", "Français"));
            options.Languages.Add(new LanguageInfo("hi", "hi", "Hindi", "in"));
            options.Languages.Add(new LanguageInfo("is", "is", "Icelandic", "is"));
            options.Languages.Add(new LanguageInfo("it", "it", "Italiano", "it"));
            options.Languages.Add(new LanguageInfo("hu", "hu", "Magyar"));
            options.Languages.Add(new LanguageInfo("pt-BR", "pt-BR", "Português"));
            options.Languages.Add(new LanguageInfo("ro-RO", "ro-RO", "Română"));
            options.Languages.Add(new LanguageInfo("ru", "ru", "Русский"));
            options.Languages.Add(new LanguageInfo("sk", "sk", "Slovak"));
            options.Languages.Add(new LanguageInfo("tr", "tr", "Türkçe"));
            options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "简体中文"));
            options.Languages.Add(new LanguageInfo("zh-Hant", "zh-Hant", "繁體中文"));
            options.Languages.Add(new LanguageInfo("de-DE", "de-DE", "Deutsch", "de"));
            options.Languages.Add(new LanguageInfo("es", "es", "Español", "es"));
        });
    }

    private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
               {
                builder
                    .WithOrigins(
                        configuration["App:CorsOrigins"]
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.RemovePostFix("/"))
                            .ToArray()
                    )
                    .WithAbpExposedHeaders()
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();

        if (!env.IsDevelopment())
        {
            app.UseErrorPage();
        }

        app.UseCorrelationId();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseJwtTokenMiddleware();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }

        app.UseUnitOfWork();
        app.UseIdentityServer();
        app.UseAuthorization();

        app.UseSwagger();
        app.UseAbpSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "BookStore API");

            var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
            c.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
            c.OAuthClientSecret(configuration["AuthServer:SwaggerClientSecret"]);
            c.OAuthScopes("BookStore");
        });

        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();
    }
}
