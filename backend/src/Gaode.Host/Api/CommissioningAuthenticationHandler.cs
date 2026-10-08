using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
namespace Gaode.Host.Api;

public sealed record CommissioningIdentity(string ProfileId, string SubjectId, string DisplayName, string Role, string Credential);
public sealed class CommissioningIdentityRegistry
{
    private readonly CommissioningIdentity[] identities;
    public CommissioningIdentityRegistry(IConfiguration config)
    {
        var records=config.GetSection("Gaode:CommissioningIdentities").GetChildren().ToArray();
        if(records.Length==0) throw new InvalidOperationException("联调身份配置缺失");
        identities=records.Select(s=> {
            string Required(string key)=>!string.IsNullOrWhiteSpace(s[key])?s[key]!:throw new InvalidOperationException("联调身份字段缺失:"+key);
            var role=Required("Role");
            if(role is not ("Operator" or "ProcessEngineer") || Required("Purpose")!="RealDeviceCommissioning")
                throw new InvalidOperationException("联调身份角色或用途无效");
            var subject=Required("SubjectId");
            if(subject.StartsWith("test:",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("联调主体不能使用Test身份");
            return new CommissioningIdentity(Required("ProfileId"),subject,Required("DisplayName"),role,Required("Credential"));
        }).ToArray();
        if(identities.Select(x=>x.ProfileId).Distinct().Count()!=identities.Length || identities.Select(x=>x.SubjectId).Distinct().Count()!=identities.Length ||
           identities.Select(x=>x.Credential).Distinct().Count()!=identities.Length || identities.Any(x=>config.GetSection("Gaode:Tokens").GetChildren().Any(t=>t.Value==x.Credential)))
            throw new InvalidOperationException("联调身份重复或复用Test凭据");
    }
    public CommissioningIdentity? Find(string supplied)=>identities.FirstOrDefault(x=>CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(x.Credential),Encoding.UTF8.GetBytes(supplied)));
    public static string[] Permissions(string role)=>role switch {
        "Operator"=>["Run.Read","Run.Start","Run.Pause","Run.Cancel","Media.Read"],
        "ProcessEngineer"=>["Run.Read","Config.Validate","Recipe.Write","Media.Read"],
        _=>[] };
}
public sealed class CommissioningAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, CommissioningIdentityRegistry identities)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header=Request.Headers.Authorization.ToString();
        if(!header.StartsWith("Bearer ",StringComparison.Ordinal)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity=identities.Find(header[7..]);
        if(identity is null) { Logger.LogWarning("CommissioningIdentityRejected {Reason} {TraceId}", "InvalidCredential", Context.TraceIdentifier); return Task.FromResult(AuthenticateResult.Fail("联调凭据无效")); }
        List<Claim> claims=[new(ClaimTypes.NameIdentifier,identity.SubjectId),new(ClaimTypes.Name,identity.DisplayName),new(ClaimTypes.Role,identity.Role),new("profileId",identity.ProfileId)];
        claims.AddRange(CommissioningIdentityRegistry.Permissions(identity.Role).Select(p=>new Claim("permission",p)));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims,Scheme.Name)),Scheme.Name)));
    }
}
