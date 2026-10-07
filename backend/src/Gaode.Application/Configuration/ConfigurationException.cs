namespace Gaode.Application.Configuration;

public sealed class ConfigurationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
