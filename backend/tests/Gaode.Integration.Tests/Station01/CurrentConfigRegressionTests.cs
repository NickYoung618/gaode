using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Cryptography;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed class CurrentConfigRegressionTests
{
    [Fact]
    public async Task SameBuildLoadsBaseAndParamAndPreservesActualAbCdNoncontiguousBatchOrder()
    {
        var root = Path.Combine(Station01HostFixture.FindWorkspace(), "specs", "008-recipe-driven-inspection", "fixtures");
        var hostFile = typeof(Gaode.Host.Api.ControlEndpoints).Assembly.Location;
        var buildHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(hostFile)));
        var folders = new List<string>();
        foreach (var name in new[] { "fixture.json", "fixture-q01-param.json", "fixture-q02.json" })
        {
            using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, name)));
            var f = fixture.RootElement;
            var slots = f.GetProperty("occupiedSlots").EnumerateArray().Select(x => x.GetString()!).ToArray();
            await using var rig = await VirtualLoopTestRig.CreateAsync(useCurrentRecipe: true,
                recipeCatalogPath: f.GetProperty("recipeCatalogPath").GetString(), occupiedSlots: slots,
                imageManifestPath: f.GetProperty("imageManifestPath").GetString(),
                workerManifestPath: f.GetProperty("workerManifestPath").GetString());
            var (request, accepted, receipt) = await rig.StartAsync();
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
            var final = await rig.WaitAsync(receipt.RunId, TimeSpan.FromMinutes(5), RunState.AwaitingManualRemoval, RunState.Blocked);
            var algorithm = rig.Host.Host.Services.GetRequiredService<IAlgorithmPort>();
            var folder = await rig.SaveEvidenceAsync("current-" + f.GetProperty("caseId").GetString()!.ToLowerInvariant(),
                request, accepted, receipt, new { final.State, final.ErrorCode, buildHash,
                    recipeRef = f.GetProperty("recipeRef"), slots, detectionCalls = algorithm.CallCount(AlgorithmRole.Detection) });
            folders.Add(folder);
            Assert.Equal(RunState.AwaitingManualRemoval, final.State);
            Assert.Equal(3 * slots.Length, algorithm.CallCount(AlgorithmRole.Detection));
            Assert.Equal(1, algorithm.CallCount(AlgorithmRole.Height));
            Assert.Equal(1, algorithm.CallCount(AlgorithmRole.FDecode));
            using var events = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "stage-events.json")));
            var payloads = events.RootElement.EnumerateArray().Select(x => JsonDocument.Parse(x.GetProperty("PayloadJson").GetString()!)).ToArray();
            try
            {
                var captures = payloads.Where(x => x.RootElement.TryGetProperty("stepSequence", out _) && x.RootElement.TryGetProperty("RelativeKey", out _))
                    .Select(x => x.RootElement).ToArray();
                var isCd = name == "fixture-q02.json";
                Assert.Equal(slots.Length * 2, captures.Length);
                Assert.Equal(Enumerable.Repeat(isCd ? "C" : "A", slots.Length)
                    .Concat(Enumerable.Repeat(isCd ? "D" : "B", slots.Length)),
                    captures.Select(x => x.GetProperty("camera").GetString()));
                Assert.All(captures, x => Assert.DoesNotContain(":P02", x.GetProperty("objectId").GetString()));
                Assert.Equal(slots.Length, captures.Select(x => x.GetProperty("objectId").GetString()).Distinct().Count());
            }
            finally { foreach (var payload in payloads) payload.Dispose(); }
            var writes = await File.ReadAllTextAsync(Path.Combine(folder, "writes.json"));
            Assert.Contains(f.GetProperty("recipeRef").GetProperty("catalogDigest").GetString()!, writes);
            if (name == "fixture-q01-param.json")
            {
                Assert.Contains("SIM_CAPTURE_AB_PARAM", writes);
                Assert.Contains("SIM_ALGORITHM_PARAM", writes);
                Assert.Contains("12000", writes);
                using var client = rig.Host.ClientForRole("Operator");
                var media = await client.GetFromJsonAsync<Gaode.Host.Api.RunMediaCatalogApi>(
                    $"/api/v1/station01/runs/{receipt.RunId:D}/media");
                var detectionMedia = media!.Items.Where(x => x.Role == "Detection").ToArray();
                Assert.Equal(slots.Length * 2, detectionMedia.Length);
                Assert.All(detectionMedia, item =>
                {
                    Assert.Equal("Ready", item.Readiness);
                    Assert.Contains("12000", item.RequestedCaptureSettings!.Value.GetRawText());
                });
            }
            Assert.Equal(buildHash, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(hostFile))));
        }
        // Both snapshots remain separate and readable after all three sequential configurations.
        Assert.All(folders, folder => Assert.True(File.Exists(Path.Combine(folder, "station01.snapshot.db"))));
        string HeightOutput(string folder) {
            using var rows = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "writes.json")));
            foreach (var row in rows.RootElement.EnumerateArray().Where(x => x.GetProperty("Kind").GetString() == "AlgorithmFact")) {
                using var payload = JsonDocument.Parse(row.GetProperty("PayloadJson").GetString()!);
                var raw = payload.RootElement.GetProperty("rawResultJson").GetString();
                if (raw?.StartsWith("[") == true) return raw;
            }
            throw new InvalidOperationException("No committed independent height output");
        }
        Assert.NotEqual(HeightOutput(folders[0]), HeightOutput(folders[1]));

    }
}
