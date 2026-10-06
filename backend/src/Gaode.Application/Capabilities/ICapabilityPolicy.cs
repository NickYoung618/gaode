using Gaode.Domain.Configuration;

namespace Gaode.Application.Capabilities;

public interface ICapabilityPolicy
{
    string Id { get; }
    string ContractVersion { get; }
    string Category { get; }
    bool SupportsPurpose(string purpose);
    string? ValidateReference(CapabilityRef reference);
}

public sealed record FixedCapabilityPolicy(string Id, string ContractVersion, string Category,
    IReadOnlySet<string> Purposes) : ICapabilityPolicy
{
    public bool SupportsPurpose(string purpose) => Purposes.Contains(purpose);
    public string? ValidateReference(CapabilityRef reference) =>
        reference.Id == Id && reference.ContractVersion == ContractVersion ? null : "CapabilityVersionMismatch";
}
