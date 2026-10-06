using System.Text.Json;
using Gaode.Plc.Protocol;

namespace FieldUi;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static readonly JsonSerializerOptions Line = new(JsonSerializerDefaults.Web);
    public static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)!;
    public static void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
}
public sealed class Configuration
{
    public int SchemaVersion { get; set; } = 2;
    public string LayoutId { get; set; } = "";
    public string Purpose { get; set; } = "Field";
    public string SourceReference { get; set; } = "2026-10-06 PC.xls + PLC(2).xls；确定内存布局";
    public string PlcProgramVersion { get; set; } = "";
    public Connection Connection { get; set; } = new();
    public Mapping Mapping { get; set; } = new();
    public bool WritesConfirmed { get; set; }
    public bool SingleWriterConfirmed { get; set; }
    public Heartbeat Heartbeat { get; set; } = new();
    public List<Point> Signals { get; set; } = [];
    public List<Axis> Axes { get; set; } = [];

    public int Address(Point p) => (p.Direction == "PC->PLC" ? Mapping.PcPduBase!.Value - 1000 : Mapping.PlcPduBase!.Value - 3000) + p.Mw;
    public string Area(Point p) => p.Direction == "PC->PLC" ? "HoldingRegister" : Mapping.PlcArea;
    public Point Point(string id) => Signals.SingleOrDefault(p => p.Id == id && p.Enabled) ?? throw new InvalidOperationException($"信号未启用：{id}");
    public void Validate(string? layoutPath = null)
    {
        if (SchemaVersion != 2) throw new InvalidOperationException("需要schemaVersion=2的配置，不能直接使用旧0.2配置。");
        if (Purpose is not ("Field" or "Virtual")) throw new InvalidOperationException("purpose须为Field或Virtual。");
        if (Connection.Protocol != "ModbusTcp") throw new InvalidOperationException("此包支持Modbus TCP；RTU串口尚未实现。");
        if (string.IsNullOrWhiteSpace(Connection.Host) || Connection.Port is not (>= 1 and <= 65535) || Connection.UnitId is not (>= 0 and <= 255))
            throw new InvalidOperationException("请填写PLC地址、端口和站号，不能用空值连接。");
        if (Purpose == "Virtual" && Connection.Host is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("Virtual配置只允许本机回环地址。");
        if (Connection.TimeoutMs is < 100 or > 3000 || Connection.PollMs is < 50 or > 2000)
            throw new InvalidOperationException("通信超时须100..3000ms，采样间隔须50..2000ms。");
        if (Purpose == "Field")
        {
            using var layout = JsonDocument.Parse(File.ReadAllText(layoutPath ?? throw new InvalidOperationException("缺少确定版点表。")));
            if (LayoutId != layout.RootElement.GetProperty("layoutId").GetString())
                throw new InvalidOperationException("点表版本过旧，请采用本包新版模板保留IP和端口重新配置；旧心跳地址不能沿用。");
            var expected = layout.RootElement.GetProperty("points").EnumerateArray().ToArray();
            if (Signals.Count != expected.Length || expected.Any(p => !Signals.Any(s =>
                s.Id == p.GetProperty("id").GetString() && s.Direction == p.GetProperty("direction").GetString() &&
                s.Mb == p.GetProperty("memoryByteAddress").GetInt32() && s.Type == p.GetProperty("valueType").GetString())))
                throw new InvalidOperationException("配置与确定版85项地址/类型不一致，请更新点表；可在高级设置调整网络映射。");
        }
        if (Mapping.PcPduBase is not (>= 0 and <= 65508) || Mapping.PlcPduBase is not (>= 0 and <= 65488))
            throw new InvalidOperationException("请填写两个块的零基PDU起点，不是40001前缀地址。");
        if (Mapping.BoolByteOrder is not ("EvenLow" or "EvenHigh") || !Enum.TryParse<Float32ByteOrder>(Mapping.FloatOrder, out _))
            throw new InvalidOperationException("请确认BOOL偶数MB所在字节及REAL四字节顺序。");
        if (Mapping.PlcArea is not ("HoldingRegister" or "InputRegister")) throw new InvalidOperationException("PLC反馈区域须选HoldingRegister或InputRegister。");
        if (Signals.Count(p => p.Enabled) == 0 || Signals.Select(p => p.Id).Distinct().Count() != Signals.Count)
            throw new InvalidOperationException("信号名称重复或没有启用点。");
        var occupied = new HashSet<string>();
        foreach (var p in Signals.Where(p => p.Enabled))
        {
            if (p.Direction is not ("PC->PLC" or "PLC->PC") || p.Type is not ("BoolByte" or "Int16" or "Float32"))
                throw new InvalidOperationException($"信号方向或类型无效：{p.Id}");
            if (p.Mb / 2 != p.Mw || (p.Type != "BoolByte" && p.Mb % 2 != 0) || Address(p) < 0 || Address(p) + p.Width > 65536)
                throw new InvalidOperationException($"地址布局无效：{p.Id}");
            if (p.WriteEnabled && p.Direction != "PC->PLC") throw new InvalidOperationException($"PLC反馈不能开放写入：{p.Id}");
            var byteOffset = Address(p) * 2 + (p.Mb % 2);
            var bytes = p.Type == "BoolByte" ? 1 : p.Width * 2;
            for (var b = 0; b < bytes; b++)
                if (!occupied.Add(Area(p) + ":" + (byteOffset + b))) throw new InvalidOperationException($"点位重叠：{p.Id}");
        }
    }
}
public sealed class Connection
{
    public string Protocol { get; set; } = "ModbusTcp";
    public string Host { get; set; } = "";
    public int? Port { get; set; }
    public int? UnitId { get; set; }
    public int TimeoutMs { get; set; } = 1000;
    public int PollMs { get; set; } = 200;
}
public sealed class Mapping
{
    public int? PcPduBase { get; set; }
    public int? PlcPduBase { get; set; }
    public string PlcArea { get; set; } = "HoldingRegister";
    public string BoolByteOrder { get; set; } = "";
    public string FloatOrder { get; set; } = "";
    public bool Confirmed { get; set; }
    public string Source { get; set; } = "";
}
public sealed class Point
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Direction { get; set; } = "";
    public int Mw { get; set; }
    public int Mb { get; set; }
    public string Type { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool WriteEnabled { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double[] AllowedValues { get; set; } = [];
    public string Note { get; set; } = "";
    public int Width => Type == "Float32" ? 2 : 1;
}
public sealed class Heartbeat
{
    public bool Confirmed { get; set; }
    public string Request { get; set; } = "PLC_Heartbeat_Req";
    public string Response { get; set; } = "PC_Heartbeat_Resp";
    public int TimeoutMs { get; set; } = 3000;
}
public sealed class Axis
{
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public string Start { get; set; } = "";
    public string Feedback { get; set; } = "";
    public string Actual { get; set; } = "";
    public bool Confirmed { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Tolerance { get; set; }
    public string Unit { get; set; } = "";
    public string Frame { get; set; } = "";
    public int? TimeoutMs { get; set; }
    public double? StartValue { get; set; }
    public double? IdleValue { get; set; }
    public double? MovingValue { get; set; }
    public double? DoneValue { get; set; }
    public double[] ErrorValues { get; set; } = [];
}
public sealed record Sample(string Signal, double? Value, string Raw, string? DecodeError, DateTimeOffset AtUtc, DateTimeOffset ChangedUtc);
public sealed record Entry(long Sequence, DateTimeOffset AtUtc, string SessionId, string Level, string Kind, string Message, string? OperationId, object? Data);
public sealed record WireEntry(long Sequence, DateTimeOffset AtUtc, string SessionId, string Direction, string Context, string? OperationId, int Transaction, int Function, string Hex, double ElapsedMs, string? Error);
public sealed class Move
{
    public string ActionId { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public Axis Axis { get; set; } = new();
    public double Target { get; set; }
    public string Phase { get; set; } = "Accepted";
    public DateTimeOffset StartedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? Error { get; set; }
}
