using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookKiosk.Tests.Cms
{
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var roleClaims = new System.Collections.Generic.List<Claim>();
            if (Request.Headers.TryGetValue("X-Test-Roles", out var rolesHeader) && !string.IsNullOrWhiteSpace(rolesHeader))
            {
                var roles = rolesHeader.ToString().Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
                foreach (var r in roles)
                {
                    roleClaims.Add(new Claim(ClaimTypes.Role, r));
                }
            }
            else
            {
                roleClaims.Add(new Claim(ClaimTypes.Role, "Staff"));
                roleClaims.Add(new Claim(ClaimTypes.Role, "Admin"));
            }

            var claims = new System.Collections.Generic.List<Claim>
            {
                new Claim(ClaimTypes.Name, "Test Staff"),
                new Claim(ClaimTypes.NameIdentifier, "staff01")
            };
            claims.AddRange(roleClaims);

            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Cms:UseMockApi", "true");
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
            });
        }
    }
}
