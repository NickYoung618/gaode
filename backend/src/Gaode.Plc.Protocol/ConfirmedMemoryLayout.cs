using System.Text.Json;

namespace Gaode.Plc.Protocol;

public sealed record ConfirmedMemoryPoint(string Id, string SourceName, string Direction,
    int MemoryByteAddress, string ValueType, string SourceType, string InitialValue,
    string Comment, string SourceFile, int SourceRow);
public sealed record ConfirmedMemorySource(string File, string Sha256);

/// <summary>User-confirmed memory addresses; network mapping is a separate field decision.</summary>
public sealed class ConfirmedMemoryLayout
{
    public const string CurrentId = "confirmed-20261006-v2";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public int SchemaVersion { get; init; }
    public string LayoutId { get; init; } = "";
    public string SourceReference { get; init; } = "";
    public ConfirmedMemorySource[] Sources { get; init; } = [];
    public ConfirmedMemoryPoint[] Points { get; init; } = [];

    public static ConfirmedMemoryLayout Load()
    {
        using var stream = typeof(ConfirmedMemoryLayout).Assembly.GetManifestResourceStream("Gaode.Plc.Protocol.confirmed-points.json")
            ?? throw new InvalidOperationException("ConfirmedMemoryLayoutMissing");
        return JsonSerializer.Deserialize<ConfirmedMemoryLayout>(stream, Json)!;
    }

    public ProtocolDefinition CreateDefinition(FieldAddressProfile profile)
    {
        profile.Validate(LayoutId);
        var fields = new List<PlcPoint>();
        foreach (var point in Points)
        {
            // Only exact existing semantic names are projected. No invented aliases for
            // Model_Number/Alarm_Code/Teach/ManualZone or other absent business signals.
            var required = ConfirmedProtocol.RequiredFields.Values.SingleOrDefault(f => f.Name == point.Id);
            if (required is null) continue;
            var type = Enum.Parse<PlcValueType>(point.ValueType);
            fields.Add(required with
            {
                DocumentNumber = profile.PduOffset(point) + 1,
                DocumentAddress = "%MB" + point.MemoryByteAddress,
                Area = PlcArea.HoldingRegister,
                ValueType = type,
                RegisterCount = type == PlcValueType.Float32 ? 2 : 1,
                ByteOffset = type == PlcValueType.BoolByte ? point.MemoryByteAddress % 2 : 0
            });
        }
        return new(fields, [], 1, 65536, Enum.Parse<Float32ByteOrder>(profile.FloatOrder))
        {
            Purpose = "Production", ByteOrderForBools = Enum.Parse<BoolByteOrder>(profile.BoolByteOrder),
            SourceReference = $"{LayoutId};{string.Join(';', Sources.Select(s => s.File + ':' + s.Sha256))};field:{profile.Source}"
        };
    }
}

public sealed class FieldAddressProfile
{
    public string LayoutId { get; init; } = "";
    public bool Confirmed { get; init; }
    public string Source { get; init; } = "";
    public int? PcPduBase { get; init; }
    public int? PlcPduBase { get; init; }
    public string PlcArea { get; init; } = "";
    public string BoolByteOrder { get; init; } = "";
    public string FloatOrder { get; init; } = "";

    public static ProtocolDefinition LoadDefinition(string path)
    {
        var profile = JsonSerializer.Deserialize<FieldAddressProfile>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("FieldAddressProfileMissing");
        return ConfirmedMemoryLayout.Load().CreateDefinition(profile);
    }

    public void Validate(string expectedLayout)
    {
        if (LayoutId != expectedLayout || !Confirmed || string.IsNullOrWhiteSpace(Source))
            throw new InvalidOperationException("FieldAddressMappingUnconfirmed:" + expectedLayout);
        if (PcPduBase is not (>= 0 and <= 65508) || PlcPduBase is not (>= 0 and <= 65488))
            throw new InvalidOperationException("FieldAddressMappingOutOfRange");
        if (PlcArea != "HoldingRegister") throw new InvalidOperationException("FormalFeedbackAreaUnsupported:" + PlcArea);
        if (!Enum.TryParse<Gaode.Plc.Protocol.BoolByteOrder>(BoolByteOrder, out var boolOrder) || !Enum.IsDefined(boolOrder) ||
            !Enum.TryParse<Float32ByteOrder>(FloatOrder, out var floatOrder) || !Enum.IsDefined(floatOrder))
            throw new InvalidOperationException("FieldByteOrderUnconfirmed");
    }

    public int PduOffset(ConfirmedMemoryPoint point) => point.Direction switch
    {
        "PC->PLC" => PcPduBase!.Value + (point.MemoryByteAddress - 2000) / 2,
        "PLC->PC" => PlcPduBase!.Value + (point.MemoryByteAddress - 6000) / 2,
        _ => throw new InvalidDataException("UnknownSignalDirection")
    };
}
