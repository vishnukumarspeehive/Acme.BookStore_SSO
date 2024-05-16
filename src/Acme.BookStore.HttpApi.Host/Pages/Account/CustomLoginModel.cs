using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using Volo.Abp.Account.Web.Pages.Account;
using Volo.Abp.Account.Web;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp;
using Microsoft.Extensions.Logging;
using Volo.Abp.Identity.AspNetCore;
using IdentityUser = Volo.Abp.Identity.IdentityUser;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Xml.Linq;

namespace Acme.BookStore.Pages.Account;

public class CustomLoginModel : LoginModel
{
    //private readonly IRepository<UserProfile, Guid> userProfileRepo;
    // IRepository<UserProfile, Guid> userProfileRepo)
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    IRepository<IdentityUser, Guid> _apbUserRepo;
    public CustomLoginModel(IAuthenticationSchemeProvider schemeProvider,
        IOptions<AbpAccountOptions> accountOptions,
        IOptions<IdentityOptions> identityOptions,
        IRepository<IdentityUser, Guid> apbUserRepo,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
        : base(schemeProvider, accountOptions, identityOptions)
    {
        _apbUserRepo = apbUserRepo;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public override Task<IActionResult> OnPostExternalLogin(string provider)
    {
        var email = HttpContext.Request.Form["emailId"].ToString();
        var emailDomain = email.Split("@")[1];

        IEnumerable<ExternalProviderModel> reConfing = ExternalProviders;
        IAuthenticationSchemeProvider authenticationSchemeProvider = HttpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();

        //Geting the current schemes (as per the our  configuration  AzureOpenIdConnect1 will be the first entry )
        IEnumerable<AuthenticationScheme> order = authenticationSchemeProvider.GetRequestHandlerSchemesAsync().Result;

        if (emailDomain == "testtenantvishnu.onmicrosoft.com")
        {
            //Arrraging the scheme order for AzureOpenIdConnect1
            authenticationSchemeProvider.RemoveScheme("AzureOpenIdConnect1");
            authenticationSchemeProvider.AddScheme(order.Where(x => x.Name == "AzureOpenIdConnect1").FirstOrDefault());

            IEnumerable<AuthenticationScheme> order2 = authenticationSchemeProvider.GetRequestHandlerSchemesAsync().Result;
            var firstSchemeName = order2.First().DisplayName;

            return base.OnPostExternalLogin("AzureOpenIdConnect2");
        }
        else if (emailDomain == "speehive.com")
        {
            //Arrraging the scheme order AzureOpenIdConnect2

            authenticationSchemeProvider.RemoveScheme("AzureOpenIdConnect2");
            authenticationSchemeProvider.AddScheme(order.Where(x => x.Name == "AzureOpenIdConnect2").FirstOrDefault());

            var order2 = authenticationSchemeProvider.GetRequestHandlerSchemesAsync().Result;
            var firstSchemeName = order2.First().DisplayName;

            return base.OnPostExternalLogin("AzureOpenIdConnect1");
        }


        return base.OnPostExternalLogin(string.Empty);
    }

    //Note I did not override the callback method
}