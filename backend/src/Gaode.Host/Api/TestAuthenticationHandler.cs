using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Gaode.Host.Api;

public sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IConfiguration config)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header) ||
            !header.ToString().StartsWith("Bearer ", StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.NoResult());
        var supplied = header.ToString()[7..];
        var roles = new[] { "Operator", "EquipmentEngineer", "ProcessEngineer", "SystemAdministrator" };
        var role = roles.FirstOrDefault(r => !string.IsNullOrEmpty(config[$"Gaode:Tokens:{r}"]) &&
            string.Equals(config[$"Gaode:Tokens:{r}"], supplied, StringComparison.Ordinal));
        if (role is null) return Task.FromResult(AuthenticateResult.Fail("测试令牌无效"));
        var permissions = role switch
        {
            "Operator" => new[] { "Run.Read", "Run.Start", "Run.Pause", "Run.Cancel", "Media.Read" },
            "EquipmentEngineer" => new[] { "Run.Read", "Run.Pause", "Run.Cancel", "Recovery.Check", "Run.Continue", "Config.Write", "Media.Read" },
            "ProcessEngineer" => new[] { "Run.Read", "Config.Validate", "Recipe.Write", "Media.Read" },
            _ => new[] { "Run.Read", "Run.Start", "Run.Pause", "Run.Cancel", "Recovery.Check", "Run.Continue", "Config.Validate", "Config.Write", "Recipe.Write", "Media.Read" }
        };
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "test:" + role), new(ClaimTypes.Role, role) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
