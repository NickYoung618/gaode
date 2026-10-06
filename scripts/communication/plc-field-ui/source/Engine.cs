using System.IO.Compression;
using System.Security.Cryptography;
using Gaode.Plc.Protocol;

namespace FieldUi;

public sealed class Engine(string root)
{
    public const string Version = "0.4.0";
    readonly SemaphoreSlim gate = new(1);
    readonly List<Entry> events = [];
    readonly List<WireEntry> wires = [];
    readonly Dictionary<string, Sample> samples = [];
    Configuration config = Json.Read<Configuration>(Path.Combine(root, File.Exists(Path.Combine(root, "site.json")) ? "site.json" : "site.template.json"));
    Wire? wire;
    StreamWriter? eventLog, wireLog;
    long eventSequence, wireSequence, txCount, rxCount, writeCount, heartbeatEdges, enableEdge;
    string status = "Disconnected", sessionId = "", runDir = "", lastError = "";
    DateTimeOffset startedUtc, lastCycle, lastHeartbeatEdge;
    double? previousHeartbeat;
    bool writesEnabled, heartbeatEnabled, heartbeatWarned;
    Move? move;
    public async Task<object> State()
    {
        await gate.WaitAsync();
        try { return new { version = Version, status, lastError, sessionId, runDirectory = runDir, config.Purpose, writesEnabled, heartbeatEnabled,
            heartbeatEdges, heartbeatLastEdgeUtc = previousHeartbeat is null ? (DateTimeOffset?)null : lastHeartbeatEdge,
            lastCycleUtc = lastCycle == default ? (DateTimeOffset?)null : lastCycle, samples = samples.Values.ToArray(),
            events = events.ToArray(), wire = wires.ToArray(), move, statistics = new { txCount, rxCount, writeCount } }; }
        finally { gate.Release(); }
    }
    public async Task<Configuration> Config()
    {
        await gate.WaitAsync(); try { return Json.Read<Configuration>(Path.Combine(root, File.Exists(Path.Combine(root, "site.json")) ? "site.json" : "site.template.json")); } finally { gate.Release(); }
    }
    public async Task SaveConfig(Configuration value)
    {
        await gate.WaitAsync();
        try
        {
            if (wire != null) throw new InvalidOperationException("请先断开，再保存配置；本轮配置快照不能中途改变。");
            if (value.SchemaVersion != 2) throw new InvalidOperationException("配置schemaVersion必须为2。");
            if (File.Exists(Path.Combine(root, "site.json")))
            {
                Directory.CreateDirectory(Path.Combine(root, "config-history"));
                File.Copy(Path.Combine(root, "site.json"), Path.Combine(root, "config-history", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".json"));
            }
            Json.Save(Path.Combine(root, "site.json"), value); config = value;
        }
        finally { gate.Release(); }
    }
    public async Task Connect()
    {
        await gate.WaitAsync();
        try
        {
            if (wire != null) throw new InvalidOperationException("当前已连接。");
            config.Validate(Path.Combine(root, "confirmed-points.json"));
            sessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];
            runDir = Path.Combine(root, "runs", sessionId); Directory.CreateDirectory(runDir);
            Json.Save(Path.Combine(runDir, "config.snapshot.json"), config);
            eventLog = new StreamWriter(Path.Combine(runDir, "events.jsonl")) { AutoFlush = true };
            wireLog = new StreamWriter(Path.Combine(runDir, "wire.jsonl")) { AutoFlush = true };
            events.Clear(); wires.Clear(); samples.Clear(); move = null; lastError = "";
            eventSequence = wireSequence = txCount = rxCount = writeCount = heartbeatEdges = enableEdge = 0;
            previousHeartbeat = null; heartbeatWarned = false; writesEnabled = heartbeatEnabled = false;
            startedUtc = DateTimeOffset.UtcNow; lastCycle = default; status = "Connecting";
            Event("Info", "Connect", "开始只读连接；没有启用任何写入。", data: new { config.Connection, config.Mapping, config.Purpose });
            if (!config.Mapping.Confirmed)
                Event("Warning", "MappingNotCalibrated", "当前按软件初值解释寄存器，尚未现场校准；原始TX/RX可查看，解释值须核对。", data: new { config.LayoutId, config.SourceReference });
            wire = new Wire(config.Connection, RecordWire);
            try
            {
                await wire.Connect(); status = "Connected";
                Event("Info", "Connected", "TCP已连接，开始读取Modbus数据。");
                await ReadCycle();
            }
            catch (Exception e) { Fail(e); throw; }
        }
        finally { gate.Release(); }
    }
    public async Task Disconnect()
    {
        await gate.WaitAsync();
        try
        {
            if (wire == null) return;
            if (move is { Phase: not ("Completed" or "Unknown") })
            {
                move.Phase = "Unknown"; move.Error = "操作员在动作期间断开，设备动作结果未知；未发送停止或清零。";
                Event("Warning", "MotionUnknown", move.Error, move.ActionId);
            }
            status = "Stopped"; Event("Info", "Stop", "连接已停止；保留最后样本。断开不是急停，没有自动清零。", move?.ActionId);
            Finish();
        }
        finally { gate.Release(); }
    }
    void RequireConnection() { if (wire == null || status != "Connected") throw new InvalidOperationException("请先只读连接并取得有效样本。"); }
    bool Active => move is { Phase: not ("Completed" or "Unknown") };
    public async Task EnableWrites(bool enabled)
    {
        await gate.WaitAsync();
        try
        {
            RequireConnection();
            if (Active) throw new InvalidOperationException("动作进行中不能改变写入开关。");
            if (enabled && (!config.WritesConfirmed || !config.SingleWriterConfirmed))
                throw new InvalidOperationException("现场配置尚未确认允许写入及没有其他上位机竞争写入。");
            writesEnabled = enabled; if (!enabled) heartbeatEnabled = false;
            Event("Info", "WritePermission", enabled ? "本轮已启用已开放PC点的写入。" : "本轮已关闭写入和心跳应答。");
        }
        finally { gate.Release(); }
    }
    public async Task EnableHeartbeat(bool enabled)
    {
        await gate.WaitAsync();
        try
        {
            RequireConnection();
            if (Active) throw new InvalidOperationException("动作进行中不能改变心跳开关。");
            if (enabled)
            {
                if (!writesEnabled || !config.Heartbeat.Confirmed || config.Heartbeat.TimeoutMs is < 300 or > 10000)
                    throw new InvalidOperationException("请先开放写入并在配置中确认心跳同值应答规则及期限。");
                var req = config.Point(config.Heartbeat.Request); var resp = config.Point(config.Heartbeat.Response);
                if (req.Type != "BoolByte" || req.Direction != "PLC->PC" || resp.Type != "BoolByte" || resp.Direction != "PC->PLC")
                    throw new InvalidOperationException("心跳必须绑定两个方向正确的BoolByte点。");
                CheckWritable(resp, 0);
                heartbeatEnabled = true; enableEdge = heartbeatEdges;
                try { await ReadCycle(); await WritePoint(resp, Value(req.Id), "heartbeat-" + Guid.NewGuid().ToString("N")[..8]); }
                catch (Exception e) { Fail(e); throw; }
            }
            else heartbeatEnabled = false;
            Event("Info", "Heartbeat", enabled ? "已启用心跳同值应答，等待后续变化。" : "已停用心跳应答。");
        }
        finally { gate.Release(); }
    }
    void CheckWritable(Point p, double value)
    {
        if (!writesEnabled || !config.WritesConfirmed || !config.SingleWriterConfirmed || !p.WriteEnabled || p.Direction != "PC->PLC")
            throw new InvalidOperationException($"该信号未获本轮写入许可：{p.Id}");
        if (!double.IsFinite(value)) throw new InvalidOperationException("不能写入非有限数值。");
        if (p.Type == "BoolByte") { if (value is not (0 or 1)) throw new InvalidOperationException("BOOL只能写0或1。"); }
        else
        {
            if (p.Type == "Int16" && (value != Math.Truncate(value) || value < short.MinValue || value > short.MaxValue))
                throw new InvalidOperationException("INT必须是-32768..32767内的整数。");
            if (p.AllowedValues.Length > 0)
            {
                if (!p.AllowedValues.Contains(value)) throw new InvalidOperationException($"{p.Id}不允许该枚举值；请核对现场含义。");
            }
            else if (p.Min is null || p.Max is null || !double.IsFinite(p.Min.Value) || !double.IsFinite(p.Max.Value) || value < p.Min || value > p.Max)
                throw new InvalidOperationException($"{p.Id}没有有效范围或数值超出范围。");
            if (p.Type == "Float32" && !float.IsFinite((float)value)) throw new InvalidOperationException("数值不能表示为REAL。");
        }
    }
    public async Task<object> ManualWrite(string id, double value)
    {
        await gate.WaitAsync();
        var op = "write-" + Guid.NewGuid().ToString("N")[..10];
        try
        {
            RequireConnection();
            if (Active) throw new InvalidOperationException("单轴动作进行中，人工写入不可并行。");
            if (heartbeatEnabled && id == config.Heartbeat.Response) throw new InvalidOperationException("自动心跳正在管理该信号，请先停用心跳再手工写。");
            var p = config.Point(id); CheckWritable(p, value);
            var axis = config.Axes.FirstOrDefault(a => a.Start == id);
            if (axis != null && (axis.IdleValue is null || value != axis.IdleValue))
                throw new InvalidOperationException("轴启动请使用单轴测试入口，以记录本次运动、到位和实测。");
            Event("Info", "WriteAccepted", $"受理人工写入 {id} = {value}。", op);
            try
            {
                var actual = await WritePoint(p, value, op);
                return new { operationId = op, requested = value, readback = actual, result = "ReadbackMatched", meaning = "仅写响应及读回一致，不表示机械动作完成。" };
            }
            catch (Exception e) { Fail(e); throw; }
        }
        catch (Exception e) { Event("Warning", "WriteRejected", e.Message, op); throw; }
        finally { gate.Release(); }
    }
    int Shift(Point p) => BoolByteCodec.Shift(p.Mb % 2, Enum.Parse<BoolByteOrder>(config.Mapping.BoolByteOrder));
    double Decode(Point p, ushort[] words)
    {
        if (p.Type == "BoolByte")
        {
            return BoolByteCodec.Decode(words[0], p.Mb % 2, Enum.Parse<BoolByteOrder>(config.Mapping.BoolByteOrder));
        }
        return p.Type == "Int16" ? unchecked((short)words[0]) : Float32Codec.Decode(words[0], words[1], Enum.Parse<Float32ByteOrder>(config.Mapping.FloatOrder));
    }
    async Task<double> WritePoint(Point p, double requested, string op)
    {
        CheckWritable(p, requested);
        var address = config.Address(p); ushort[] values;
        if (p.Type == "BoolByte")
        {
            var before = await wire!.Read(address, 1, "HoldingRegister", $"{p.Id}:读原字保留邻字节", op);
            var shift = Shift(p);
            values = [BoolByteCodec.Merge(before[0], (ushort)requested, p.Mb % 2, Enum.Parse<BoolByteOrder>(config.Mapping.BoolByteOrder))];
            Event("Info", "ByteMerge", $"{p.Id}只改自己的字节，保留同字邻居。", op, new { before = before[0], after = values[0], shift, p.Mb });
        }
        else if (p.Type == "Int16") values = [unchecked((ushort)(short)requested)];
        else values = Float32Codec.Encode((float)requested, Enum.Parse<Float32ByteOrder>(config.Mapping.FloatOrder));
        Event("Info", "WriteDispatch", $"发送 {p.Id} = {requested}。", op, new { pduAddress = address, words = values });
        await wire!.Write(address, values, p.Id + ":写入", op);
        Event("Info", "WriteAcknowledged", $"PLC已应答 {p.Id} 写请求，尚待读回。", op);
        var back = await wire.Read(address, values.Length, "HoldingRegister", p.Id + ":读回", op);
        UpdateSample(p, back, op);
        if (!back.SequenceEqual(values)) throw new IOException($"{p.Id}读回与发送字不一致；不能记为通过。若PLC主动清零，需核对该点握手规则。");
        var decoded = Decode(p, back);
        Event("Info", "ReadbackMatched", $"{p.Id}读回一致：{decoded}。这不是机械完成确认。", op, new { requested, readback = decoded });
        return decoded;
    }
    double Value(string id) => samples.TryGetValue(id, out var s) && s.Value is not null ? s.Value.Value : throw new InvalidOperationException($"没有有效反馈：{id}");
    void CheckGuards()
    {
        if (Value("PLC_Ready_State") != 1 || Value("PLC_Mode_Auto") != 1 || Value("PLC_System_Fault") != 0)
            throw new InvalidOperationException("动作条件不满足：需要PLC就绪、自动、无故障。");
        if (!heartbeatEnabled || heartbeatEdges <= enableEdge || (DateTimeOffset.UtcNow - lastHeartbeatEdge).TotalMilliseconds > config.Heartbeat.TimeoutMs)
            throw new InvalidOperationException("尚未取得启用应答后的有效心跳变化。");
    }
    public async Task<object> StartMove(string name, double target)
    {
        await gate.WaitAsync();
        try
        {
            RequireConnection();
            if (Active || move?.Phase == "Unknown") throw new InvalidOperationException("已有未完成或结果未知的动作；请现场处置后重新建立会话。");
            var a = config.Axes.SingleOrDefault(x => x.Name == name) ?? throw new InvalidOperationException("没有该轴配置。");
            if (!a.Confirmed || a.Min is null || a.Max is null || a.Tolerance is null || a.Tolerance < 0 || !double.IsFinite(a.Tolerance.Value) ||
                a.TimeoutMs is not (>= 200 and <= 300000) || string.IsNullOrWhiteSpace(a.Unit) || string.IsNullOrWhiteSpace(a.Frame) ||
                a.StartValue is null || a.IdleValue is null || a.StartValue == a.IdleValue || a.MovingValue is null || a.DoneValue is null || a.MovingValue == a.DoneValue)
                throw new InvalidOperationException("该轴尚未确认完整范围、容差、坐标系、期限和握手码，请由现场Codex补充配置。");
            if (!double.IsFinite(target) || !double.IsFinite(a.Min.Value) || !double.IsFinite(a.Max.Value) || target < a.Min || target > a.Max || Math.Abs((double)(float)target - target) > a.Tolerance)
                throw new InvalidOperationException("目标超出轴范围或REAL编码误差大于容差。");
            var tp = config.Point(a.Target); var sp = config.Point(a.Start); var fp = config.Point(a.Feedback); var ap = config.Point(a.Actual);
            if (tp.Type != "Float32" || sp.Type != "BoolByte" || fp.Type != "Int16" || ap.Type != "Float32" || fp.Direction != "PLC->PC" || ap.Direction != "PLC->PC")
                throw new InvalidOperationException("轴的目标/启动/反馈/实际值类型或方向不符。");
            CheckWritable(tp, target); CheckWritable(sp, a.StartValue.Value); CheckWritable(sp, a.IdleValue.Value);
            try { await ReadCycle(); } catch (Exception e) { Fail(e); throw; }
            CheckGuards();
            if (Value(a.Start) != a.IdleValue) throw new InvalidOperationException("轴启动信号未处于已确认的空闲值。");
            move = new Move { Axis = a, Target = target };
            Event("Info", "MotionAccepted", $"受理{name}单轴目标{target} {a.Unit}，坐标系{a.Frame}。", move.ActionId);
            try
            {
                await WritePoint(tp, target, move.ActionId); move.Phase = "TargetWritten";
                Event("Info", "MotionPhase", "目标已写入并读回。", move.ActionId);
                await WritePoint(sp, a.StartValue.Value, move.ActionId); move.Phase = "AwaitMoving";
                Event("Info", "MotionPhase", "启动已写入，等待本次运动中反馈；旧到位不算完成。", move.ActionId);
                return new { move.ActionId, move.Phase };
            }
            catch (Exception e) { Fail(e); throw; }
        }
        catch (Exception e) { Event("Warning", "MotionRejected", e.Message); throw; }
        finally { gate.Release(); }
    }
    async Task AdvanceMove()
    {
        if (!Active) return;
        var m = move!; var a = m.Axis;
        CheckGuards();
        if ((DateTimeOffset.UtcNow - m.StartedUtc).TotalMilliseconds > a.TimeoutMs) throw new TimeoutException("单轴观察超时，结果未知；没有重发或自动清零。");
        var feedback = Value(a.Feedback);
        if (a.ErrorValues.Contains(feedback)) throw new IOException($"PLC轴反馈失败码：{feedback}。");
        if (m.Phase == "AwaitMoving" && feedback == a.MovingValue)
        {
            m.Phase = "AwaitDone"; Event("Info", "MotionPhase", "已看到本次运动中反馈，等待到位。", m.ActionId);
        }
        else if (m.Phase == "AwaitDone" && feedback == a.DoneValue)
        {
            var p = config.Point(a.Actual); var actual = await wire!.Read(config.Address(p), p.Width, config.Area(p), p.Id + ":完成实测", m.ActionId);
            UpdateSample(p, actual, m.ActionId); var pos = Value(a.Actual);
            if (Math.Abs(pos - m.Target) > a.Tolerance) throw new IOException($"到位后实测{pos}与目标{m.Target}不在容差内。");
            Event("Info", "MotionPhase", $"已到位，实测{pos}符合容差；开始清零启动信号。", m.ActionId);
            await WritePoint(config.Point(a.Start), a.IdleValue!.Value, m.ActionId);
            m.Phase = "Completed";
            Event("Info", "MotionCompleted", $"{a.Name}本次运动、到位、实测及清零已观察通过。", m.ActionId, new { target = m.Target, actual = pos });
        }
    }
    void UpdateSample(Point p, ushort[] words, string? op)
    {
        var now = DateTimeOffset.UtcNow; var raw = string.Join(" ", words.Select(x => x.ToString("X4")));
        double? value = null; string? error = null;
        try { value = Decode(p, words); } catch (Exception e) { error = e.Message; }
        // Shared-word neighbours can change the raw word without changing this signal.
        var changed = !samples.TryGetValue(p.Id, out var prior) || prior.Value != value || prior.DecodeError != error;
        var sampled = new Sample(p.Id, value, raw, error, now, changed ? now : prior!.ChangedUtc); samples[p.Id] = sampled;
        if (changed) Event(error is null ? "Info" : "Warning", "SignalChanged", $"{p.Id} = {(value is null ? error : value)}；原始字 {raw}。", op, sampled);
    }
    async Task ReadCycle()
    {
        // Merge only configured occupied words; a BOOL may share its word with padding.
        foreach (var group in config.Signals.Where(p => p.Enabled).GroupBy(config.Area))
        {
            var points = group.OrderBy(config.Address).ToArray();
            var index = 0;
            while (index < points.Length)
            {
                var from = config.Address(points[index]); var end = from + points[index].Width;
                var included = new List<Point> { points[index++] };
                while (index < points.Length && config.Address(points[index]) <= end && config.Address(points[index]) + points[index].Width - from <= 125)
                { end = Math.Max(end, config.Address(points[index]) + points[index].Width); included.Add(points[index++]); }
                var words = await wire!.Read(from, end - from, group.Key, "轮询:" + string.Join(",", included.Select(p => p.Id)), Active ? move!.ActionId : null);
                foreach (var p in included) UpdateSample(p, words.Skip(config.Address(p) - from).Take(p.Width).ToArray(), Active ? move!.ActionId : null);
            }
        }
        lastCycle = DateTimeOffset.UtcNow;
        if (samples.TryGetValue(config.Heartbeat.Request, out var hb) && hb.Value is not null)
        {
            var value = hb.Value.Value;
            if (previousHeartbeat is null || value != previousHeartbeat)
            {
                if (previousHeartbeat is not null) heartbeatEdges++;
                previousHeartbeat = value; lastHeartbeatEdge = DateTimeOffset.UtcNow; heartbeatWarned = false;
                if (heartbeatEnabled) await WritePoint(config.Point(config.Heartbeat.Response), value, "heartbeat-" + Guid.NewGuid().ToString("N")[..8]);
            }
            else if ((DateTimeOffset.UtcNow - lastHeartbeatEdge).TotalMilliseconds > config.Heartbeat.TimeoutMs)
            {
                if (heartbeatEnabled) throw new TimeoutException("PLC心跳超过配置期限未变化；应答已停止，动作结果以现场为准。");
                if (!heartbeatWarned) { Event("Warning", "HeartbeatStale", "只读观察：PLC心跳超过配置期限未变化。"); heartbeatWarned = true; }
            }
        }
        else if (heartbeatEnabled) throw new IOException("心跳信号没有可解码的有效值。");
    }
    public async Task Poll(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            var delay = 200;
            await gate.WaitAsync(stop);
            try
            {
                if (wire != null && status == "Connected")
                {
                    try { await ReadCycle(); await AdvanceMove(); }
                    catch (Exception e) { Fail(e); }
                    delay = Active ? 50 : config.Connection.PollMs;
                }
            }
            finally { gate.Release(); }
            await Task.Delay(delay, stop);
        }
    }
    void RecordWire(string direction, string context, string? op, int tid, int function, string hex, double elapsed, string? error)
    {
        var e = new WireEntry(++wireSequence, DateTimeOffset.UtcNow, sessionId, direction, context, op, tid, function, hex, Math.Round(elapsed, 2), error);
        wireLog!.WriteLine(System.Text.Json.JsonSerializer.Serialize(e, Json.Line));
        wires.Add(e); if (wires.Count > 160) wires.RemoveAt(0);
        if (direction == "TX") { txCount++; if (function is 6 or 16) writeCount++; }
        if (direction == "RX") rxCount++;
    }
    void Event(string level, string kind, string message, string? op = null, object? data = null)
    {
        var e = new Entry(++eventSequence, DateTimeOffset.UtcNow, sessionId, level, kind, message, op, data);
        eventLog?.WriteLine(System.Text.Json.JsonSerializer.Serialize(e, Json.Line));
        events.Add(e); if (events.Count > 120) events.RemoveAt(0);
    }
    void Fail(Exception error)
    {
        status = "Failed"; lastError = error.Message;
        if (Active) { move!.Phase = "Unknown"; move.Error = error.Message; }
        Event("Error", "Failure", error.Message, move?.ActionId);
        Finish();
    }
    void Finish()
    {
        wire?.Dispose(); wire = null; writesEnabled = heartbeatEnabled = false;
        if (runDir.Length > 0) Json.Save(Path.Combine(runDir, "summary.json"), new { schemaVersion = 2, toolVersion = Version, sessionId, config.Purpose,
            startedUtc, endedUtc = DateTimeOffset.UtcNow, status, error = lastError, configSha256 = Hash(Path.Combine(runDir, "config.snapshot.json")),
            statistics = new { txCount, rxCount, writeCount, heartbeatEdges }, move,
            interpretation = "人工写读回只证明通信观察；Completed单轴亦不代表整机验收。缺summary的会话为中断，不能视为通过。" });
        eventLog?.Dispose(); wireLog?.Dispose(); eventLog = wireLog = null;
    }
    public object Notes() => File.Exists(Path.Combine(root, "field-notes.json")) ? Json.Read<object>(Path.Combine(root, "field-notes.json")) : new { plcModel = "", programVersion = "", @operator = "", observations = "", remainingQuestions = "", localCodeChanges = "", cases = Array.Empty<object>() };
    public void SaveNotes(object value) => Json.Save(Path.Combine(root, "field-notes.json"), value);
    public static string Hash(string path) { using var f = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant(); }
    public async Task<object> Export()
    {
        await gate.WaitAsync();
        try
        {
            if (wire != null) throw new InvalidOperationException("请先断开并保存本轮结果，再导出回传包。");
            var directory = Path.Combine(root, "exports"); Directory.CreateDirectory(directory);
            var filename = "PLC-return-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6] + ".zip";
            var paths = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(p =>
            {
                var rel = Path.GetRelativePath(root, p).Replace('\\', '/');
                return !rel.StartsWith("exports/") && !rel.Contains("/bin/") && !rel.Contains("/obj/") && !rel.Contains("/__pycache__/") && !rel.StartsWith("tests/") && !rel.EndsWith(".zip");
            }).Order().ToArray();
            var manifest = new { schemaVersion = 2, toolVersion = Version, exportedUtc = DateTimeOffset.UtcNow,
                note = "现场原始回传；包括成功、失败、中断和本地文件；未自动上传。", files = paths.Select(p => new { path = Path.GetRelativePath(root, p).Replace('\\', '/'), bytes = new FileInfo(p).Length, sha256 = Hash(p) }).ToArray() };
            var destination = Path.Combine(directory, filename);
            using (var zip = ZipFile.Open(destination, ZipArchiveMode.Create))
            {
                foreach (var p in paths) zip.CreateEntryFromFile(p, Path.GetRelativePath(root, p).Replace('\\', '/'), CompressionLevel.Fastest);
                using var writer = new StreamWriter(zip.CreateEntry("return-manifest.json").Open()); writer.Write(System.Text.Json.JsonSerializer.Serialize(manifest, Json.Options));
            }
            return new { filename, path = destination, sha256 = Hash(destination), download = "/api/download/" + filename };
        }
        finally { gate.Release(); }
    }
}
