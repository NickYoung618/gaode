using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gaode.Application.Configuration;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Domain.Configuration;

namespace Gaode.Infrastructure.Simulation;

public static class CommissioningAlgorithmInputs
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static LoadedConfiguration<CommissioningConfiguration> Load(string path, string expectedSha256)
    {
        if (!Path.IsPathFullyQualified(path) || string.IsNullOrWhiteSpace(expectedSha256))
            throw new InvalidDataException("CommissioningConfigurationPathAndHashRequired");
        var bytes = File.ReadAllBytes(path);
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(digest, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("CommissioningConfigurationDigestMismatch");
        var json = new UTF8Encoding(false, true).GetString(bytes);
        var value = JsonSerializer.Deserialize<CommissioningConfiguration>(json, Json)
            ?? throw new InvalidDataException("CommissioningConfigurationMissing");
        ValidateIdentity(value);
        return new(value, json, digest, path);
    }
    public static void ValidateIdentity(CommissioningConfiguration value)
    {
        var recipe = value.ExpectedRecipe;
        if (value.SchemaVersion != "020-commissioning/1" || value.Purpose != RuntimePurposes.RealDeviceCommissioning ||
            new[] { value.Id, value.Version, value.Source, recipe?.RecipeId, recipe?.Version, recipe?.DefinitionDigest,
                recipe?.ScenarioId, recipe?.Model, recipe?.FCode, value.PublicConfigRef?.Id, value.PublicConfigRef?.Version,
                value.BudgetRef?.Id, value.BudgetRef?.Version, value.CodeRule?.Source }.Any(string.IsNullOrWhiteSpace) ||
            value.CodeRule is not { Id: "decoded-content-exact", Version: "1.0" } ||
            value.PublicLightChannels is null || value.PublicLightChannels.Any(p => string.IsNullOrWhiteSpace(p.Key) || string.IsNullOrWhiteSpace(p.Value)) ||
            value.Algorithms is null || value.Slots is null || value.RawCodes is null || value.Results is null)
            throw new InvalidDataException("CommissioningConfigurationIdentityOrSourceInvalid");
        foreach (var code in value.EntityCodes ?? [])
            if (new[] { code.Id, code.Version, code.Source }.Any(string.IsNullOrWhiteSpace) || code.Scope is null ||
                code.Codes is null || code.Codes.Count != 1 || code.Codes.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("CommissioningEntityCodeInputInvalid");
        foreach (var input in value.RecipeInputs ?? [])
            ValidateIdentity(value with { Id = input.Id, Version = input.Version, Source = input.Source,
                ExpectedRecipe = input.ExpectedRecipe, Slots = input.Slots, FLocation = input.FLocation,
                MappingSourceReference = input.MappingSourceReference, RawCodes = input.RawCodes,
                Results = input.Results, EntityCodes = input.EntityCodes, RecipeInputs = null });
        var identities = (value.RecipeInputs ?? []).Select(i => (i.ExpectedRecipe.RecipeId, i.ExpectedRecipe.Version))
            .Append((value.ExpectedRecipe.RecipeId, value.ExpectedRecipe.Version)).ToArray();
        if (identities.Distinct().Count() != identities.Length) throw new InvalidDataException("CommissioningRecipeInputsDuplicate");
        foreach (var binding in value.Algorithms)
        {
            var expected = binding.Purpose switch
            {
                AlgorithmPurpose.TrayPose => (1, "tray-observation/2"),
                AlgorithmPurpose.TrayCode or AlgorithmPurpose.EntityCode => (1, "decoded-code/1"),
                AlgorithmPurpose.SingleDetection => (1, "image-quality/1"),
                AlgorithmPurpose.FaceFusion => (2, "face-quality/1"),
                _ => throw new InvalidDataException("CommissioningAlgorithmCapabilityUnsupported")
            };
            if (binding.InputCount != expected.Item1 || binding.ResultContract != expected.Item2 ||
                new[] { binding.CapabilityId, binding.CapabilityVersion, binding.ParametersVersion }.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("CommissioningAlgorithmCapabilityInvalid");
        }
        if (value.Algorithms.Select(b => b.Purpose).Distinct().Count() != value.Algorithms.Count)
            throw new InvalidDataException("CommissioningAlgorithmCapabilityDuplicate");
        foreach (var result in value.Results)
            if (new[] { result.Id, result.Version, result.Source }.Any(string.IsNullOrWhiteSpace) ||
                result.Purpose is not (AlgorithmPurpose.SingleDetection or AlgorithmPurpose.FaceFusion) ||
                result.Scope is null || result.Disposition is not ("OK" or "NG" or "Pending"))
                throw new InvalidDataException("CommissioningAlgorithmResultInvalid");
    }
}
