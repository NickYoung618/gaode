using System.Text.Json;
using Gaode.Application.Station01;

namespace Gaode.Integration.Tests.Support;

public static class StartRunContextJson
{
    public static string Create(string scenarioId = "S1", Guid? trayId = null,
        IReadOnlyList<string>? occupiedSlots = null, string purpose = "Test") =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = StartRunContext.CurrentSchemaVersion,
            trayId = trayId ?? Guid.NewGuid(),
            stationId = "10000000-0000-0000-0000-000000000001",
            lineId = "20000000-0000-0000-0000-000000000001",
            scenarioId,
            occupiedSlots = occupiedSlots ?? Enumerable.Range(1, 15).Select(i => $"P{i:00}").ToArray(),
            purpose
        });
}
