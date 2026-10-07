using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Gaode.Host.Api;

public static class Station01Authorization
{
    public const string Read = "Run.Read";
    public const string Start = "Run.Start";
    public const string Pause = "Run.Pause";
    public const string Cancel = "Run.Cancel";
    public const string RecoveryCheck = "Recovery.Check";
    public const string Continue = "Run.Continue";
    public const string ConfigValidate = "Config.Validate";
    public const string ConfigWrite = "Config.Write";
    public const string RecipeWrite = "Recipe.Write";
    public const string MediaRead = "Media.Read";
    public static IServiceCollection AddStation01Api(this IServiceCollection services, IConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config["Gaode:Tokens:Operator"]))
            throw new InvalidOperationException("必须显式配置本地Test令牌");
        services.AddAuthentication("Station01Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Station01Test", _ => { });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Read, p => p.RequireClaim("permission", Read));
            options.AddPolicy(Start, p => p.RequireClaim("permission", Start));
            options.AddPolicy(Pause, p => p.RequireClaim("permission", Pause));
            options.AddPolicy(Cancel, p => p.RequireClaim("permission", Cancel));
            options.AddPolicy(RecoveryCheck, p => p.RequireClaim("permission", RecoveryCheck));
            options.AddPolicy(Continue, p => p.RequireClaim("permission", Continue));
            options.AddPolicy(ConfigValidate, p => p.RequireClaim("permission", ConfigValidate));
            options.AddPolicy(ConfigWrite, p => p.RequireClaim("permission", ConfigWrite));
            options.AddPolicy(RecipeWrite, p => p.RequireClaim("permission", RecipeWrite));
            options.AddPolicy(MediaRead, p => p.RequireClaim("permission", MediaRead));
        });
        services.AddSignalR();
        return services;
    }
}
