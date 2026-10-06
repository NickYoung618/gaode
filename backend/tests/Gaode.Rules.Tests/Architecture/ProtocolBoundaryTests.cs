using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Gaode.Rules.Tests.Architecture;

public sealed class ProtocolBoundaryTests
{
    private static readonly Dictionary<string, HashSet<string>> Shapes = new()
    {
        ["Gaode.Application.Ports.Observation"] = ["Clamp", "Slot", "X", "DueTick", "EvidenceReference"],
        ["Gaode.Application.Ports.ClampState"] = ["Unconfirmed", "Secured", "Released"]
    };
    private const string Prefix = "global using System; global using System.Text.Json; ";

    [Fact]
    public void CurrentCompositionOptionsRemainAcceptedWithoutRetiredPollingKnob()
    {
        PositiveCaseIsActuallyParsedAndChecked("013-composition", "composition-api",
            "namespace Gaode.Infrastructure.Devices.Plc { public class PlcRuntimeOptions { public int IoTimeoutMs {get;set;} } public class LatestProtocolPlcDevice(PlcRuntimeOptions o) {} } class Host { object Build() { var o=new Gaode.Infrastructure.Devices.Plc.PlcRuntimeOptions { IoTimeoutMs=1000 }; return new Gaode.Infrastructure.Devices.Plc.LatestProtocolPlcDevice(o); } }");
        Assert.Contains(Check("namespace Gaode.Infrastructure.Devices.Plc { public class PlcRuntimeOptions { public int PollMs {get;set;} } } class Host { object Build()=>new Gaode.Infrastructure.Devices.Plc.PlcRuntimeOptions { PollMs=50 }; }", "composition-api"),
            violation => violation.RuleId is "A03" or "A04");
    }
    [Fact]
    public void CommunicationCollectionPolicyCannotEnterTheCommonBusinessLayer()
    {
        NegativeCaseUsesProductionChecker("013-policy-boundary", "A04", "business",
            "namespace Gaode.Infrastructure.Devices.Plc { public class PlcAcquisitionPolicy { public int FeedbackMs {get;} } } class Flow { int Run(Gaode.Infrastructure.Devices.Plc.PlcAcquisitionPolicy p)=>p.FeedbackMs; }");
    }

    [Theory]
    [InlineData("N01-address", "A04", "business", "class Transport { public void WriteRegisterAsync(int address,int value) {} } class Flow { void Run(Transport t) => t.WriteRegisterAsync(0x52,2); }")]
    [InlineData("N02-renamed-state", "A02", "business", "namespace Gaode.Application.Ports { public record Observation(int DevicePhase); } class Flow { bool Done(Gaode.Application.Ports.Observation s) => s.DevicePhase==2; }")]
    [InlineData("N02-renamed-state/nested", "A02", "business", "namespace Gaode.Application.Ports { public record Observation(Hidden Clamp); public record Hidden(int Phase); }")]
    [InlineData("N02-renamed-state/json", "A02", "business", "namespace Gaode.Application.Ports { public record Observation(JsonElement Clamp); }")]
    [InlineData("N03-cast", "A03", "business", "namespace Gaode.Plc.Protocol { public enum Wire { Done=2 } } enum Business { Done } class Flow { bool Done(Gaode.Plc.Protocol.Wire raw) => (Business)raw==Business.Done; }")]
    [InlineData("N04-bit", "A03", "business", "class Observation { public int AlarmBits {get;set;} } class Flow { bool Ready(Observation o) { var renamed=o.AlarmBits; return (renamed & 2)==0; } }")]
    [InlineData("N05-offset", "A03", "business", "class Observation { public byte[] RawBytes {get;set;}=[]; } class Flow { bool Done(Observation o) => o.RawBytes[4]==2; }")]
    [InlineData("N06-handshake", "A04", "business", "class Observation { public bool SortingAckCleared {get;set;} } class Flow { bool Done(Observation o) => o.SortingAckCleared; }")]
    [InlineData("N06-handshake/automatic-fact", "A04", "business", "class Flow { object Fact() => new { kind=\"FlipAckCleared\" }; }")]
    [InlineData("N06-handshake/manual-fact", "A04", "business", "class Flow { object Fact() => new { kind=\"ManualFlipCompletionCleared\" }; }")]
    [InlineData("N06-handshake/acquisition-fact", "A04", "business-orchestration", "class Flow { object Fact() => new { kind=\"DetectionResetConfirmed\" }; }")]
    [InlineData("N08-orchestration-protected", "A04", "business-orchestration", "namespace Gaode.Infrastructure.Devices.Plc { public record State(int Value); } class Flow { bool Done(Gaode.Infrastructure.Devices.Plc.State state)=>state.Value==2; }")]
    [InlineData("N07-diagnostics", "A05", "business", "class Flow { bool Done(JsonElement json) => json.GetProperty(\"ProtocolStatus\").GetInt32()==3; }")]
    [InlineData("N08-business-test", "A07", "business-test", "using Gaode.Plc.Protocol; namespace Gaode.Plc.Protocol { public class Wire {} } class Contract { Wire w=new(); }")]
    [InlineData("N08-business-wire-helper", "A04", "business-test", "class Fixture { public int AcceptedWriteCount(int documentNumber,ushort value)=>0; } class Test { bool Done(Fixture rig)=>rig.AcceptedWriteCount(0x0001,2)==1; }")]
    [InlineData("N10-bypass", "A06", "communication-implementation", "class Transport { public int[] ReadRegisters(int offset,int count)=>[]; } class Adapter { int Poll(Transport t)=>t.ReadRegisters(0,90)[81]; }")]
    [InlineData("N11-composition-raw", "A04", "composition-api", "namespace Gaode.Infrastructure.Devices.Plc { public class LatestProtocolPlcDevice { public int RawState()=>2; } } class Host { bool Done(Gaode.Infrastructure.Devices.Plc.LatestProtocolPlcDevice d)=>d.RawState()==2; }")]
    [InlineData("N11-composition-helper", "A03", "composition-api", "namespace Gaode.Infrastructure.Devices.Plc { public class LatestProtocolPlcDevice {} } class Host { object Pass(object o)=>o; object Leak(Gaode.Infrastructure.Devices.Plc.LatestProtocolPlcDevice d)=>Pass(d); }")]
    [InlineData("N03-protocol-version-literal", "A04", "business", "class Flow { string Version()=>\"plc-upper-20260925-partitioned-ack\"; }")]
    [InlineData("N07-qualified-reader", "A05", "business", "namespace Gaode.Infrastructure.Persistence { public record CommunicationEvidenceDocument(string Payload); } class Flow { bool Done(Gaode.Infrastructure.Persistence.CommunicationEvidenceDocument renamed)=>renamed.Payload==\"ok\"; }")]
    [InlineData("N07-qualified-exchange", "A03", "business", "namespace Gaode.Infrastructure.Persistence { public record RawExchange(string ResponseHex); } class Flow { bool Done(Gaode.Infrastructure.Persistence.RawExchange renamed)=>renamed.ResponseHex==\"ok\"; }")]
    public void NegativeCaseUsesProductionChecker(string caseId, string expectedRule, string role, string source)
    {
        var violations = Check(source, role);
        Assert.DoesNotContain(violations, x => x.RuleId == "CHECKER-BIND");
        Assert.Contains(violations, x => x.RuleId == expectedRule && x.Line > 0 && x.Column > 0);
        Assert.NotEmpty(caseId);
    }

    [Fact]
    public void N09BusinessContractIsRejectedByTheSameDocumentRule()
    {
        var checker = new ProtocolBoundaryChecker(Shapes);
        Assert.Contains(checker.CheckContract("contract.md", "## 业务接口\n| 字段 | 类型 |\n| ProtocolStatus | int |"),
            x => x.RuleId == "A07" && x.Line == 3);
        Assert.Contains(checker.CheckContract("contract.md", "## 业务接口\n```json\n{\"ProtocolStatus\":3}\n```"),
            x => x.RuleId == "A07" && x.Line == 3);
        Assert.Empty(checker.CheckContract("contract.md", "## 历史研究\n禁止业务依赖ProtocolStatus。\n| ProtocolStatus | 原始历史值 |"));
    }

    [Theory]
    [InlineData("P01-business-numbers", "business", "class Plan { bool Valid(int face,int slot)=>face is 1 or 2 or 4 && slot>0; }")]
    [InlineData("P02-coordinate-time", "business", "class Motion { bool Ready(double actual,double target,double tolerance,long now,long due)=>Math.Abs(actual-target)<=tolerance && now<due; }")]
    [InlineData("P03-semantics", "business", "namespace Gaode.Application.Ports { public enum ClampState { Unconfirmed,Secured,Released } public record Observation(ClampState Clamp); } class Flow { bool Ready(Gaode.Application.Ports.Observation o)=>o.Clamp==Gaode.Application.Ports.ClampState.Secured; }")]
    [InlineData("P04-actual-storage-fixture", "business-orchestration", "namespace Gaode.Infrastructure.Persistence { public record SavedBusinessFact(string Kind); } class Test { bool Current(Gaode.Infrastructure.Persistence.SavedBusinessFact fact)=>fact.Kind==\"AcquisitionReleased\"; }")]
    [InlineData("P04-business-payload", "business", "class Image { double[] Read(JsonElement data)=>[data.GetProperty(\"height\").GetDouble()]; }")]
    [InlineData("P05-opaque-reference", "business", "class Flow { string Preserve(string evidenceReference)=>evidenceReference; }")]
    [InlineData("P03-semantic-face-fact", "business", "class Flow { object Fact(bool manual) => new { kind=manual ? \"ManualFaceEstablished\" : \"FaceEstablished\" }; }")]
    [InlineData("P05-producer-version", "business", "record Origin(string ComponentVersion); class Flow { string Preserve(Origin origin)=>origin.ComponentVersion; }")]
    [InlineData("P06-transport-layout", "communication-transport", "class Codec { int Read(byte[] packet)=>(packet[0]<<8)|packet[1]; }")]
    [InlineData("P07-composition-lifecycle", "composition-api", "namespace Gaode.Infrastructure.Devices.Plc { public class LatestProtocolPlcDevice { public void StartAsync() {} public void DisposeAsync() {} } } class Host(Gaode.Infrastructure.Devices.Plc.LatestProtocolPlcDevice d) { void Start()=>d.StartAsync(); void Stop()=>d.DisposeAsync(); }")]
    [InlineData("P04-semantic-historical-name", "business-test", "class InspectionHandshakeSequenceTests { bool Reliable(bool hasCurrentObservation) { var current=hasCurrentObservation; return current; } }")]
    [InlineData("P04-storage-helper-name", "business-test", "class CommunicationEvidenceStoreTests { public static string StorePath()=>\"test.db\"; } class SaveTests { string Open()=>CommunicationEvidenceStoreTests.StorePath(); }")]
    [InlineData("P08-composition-options", "composition-api", "namespace Gaode.Infrastructure.Devices.Plc { public class PlcRuntimeOptions { public int IoTimeoutMs {get;set;} } public class LatestProtocolPlcDevice(PlcRuntimeOptions o) {} } class Host { object Build() { var o=new Gaode.Infrastructure.Devices.Plc.PlcRuntimeOptions { IoTimeoutMs=1000 }; return new Gaode.Infrastructure.Devices.Plc.LatestProtocolPlcDevice(o); } }")]
    public void PositiveCaseIsActuallyParsedAndChecked(string caseId, string role, string source)
    {
        Assert.Empty(Check(source, role));
        Assert.NotEmpty(caseId);
    }

    [Theory]
    [InlineData("N18-protected-path/application", "backend/src/Gaode.Application/NewFlow.cs")]
    [InlineData("N18-protected-path/domain", "backend/src/Gaode.Domain/NewPolicy.cs")]
    [InlineData("N18-protected-path/host", "backend/src/Gaode.Host/Api/NewEndpoint.cs")]
    [InlineData("N18-protected-path/recipe", "backend/src/Gaode.Infrastructure/Recipes/JsonRecipeCatalog.cs")]
    [InlineData("N18-protected-path/state", "backend/src/Gaode.Infrastructure/Simulation/SimulatedDeviceState.cs")]
    [InlineData("N18-protected-path/unavailable", "backend/src/Gaode.Infrastructure/Integrations/NotIntegratedStagePorts.cs")]
    [InlineData("N18-protected-path/integrated", "backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs")]
    public void ProtectedClassificationCannotHideWireKnowledge(string caseId, string path)
    {
        var role = ProtocolBoundaryChecker.ProtectedRole(path, "infrastructure-consumer");
        const string source = "class Transport { public void WriteRegisterAsync(int address,int value) {} } class Flow { void Run(Transport t) => t.WriteRegisterAsync(0x52,2); }";
        Assert.Contains(Check(source, role), x => x.RuleId == "A04");
        Assert.NotEmpty(caseId);
    }

    [Fact]
    public void P09LegitimateCommunicationDiagnosticRetainsItsResponsibility()
    {
        var role = ProtocolBoundaryChecker.ProtectedRole(
            "backend/src/Gaode.Host/Api/CommunicationDiagnosticEndpoints.cs", "communication-diagnostics");
        Assert.Equal("communication-diagnostics", role);
        Assert.Empty(Check("class Diagnostic { public byte[] RawBytes { get; set; } = []; }", role));
    }

    private static IReadOnlyList<BoundaryViolation> Check(string source, string role)
    {
        var tree = CSharpSyntaxTree.ParseText(Prefix + source, path: "fixture.cs");
        var compilation = CSharpCompilation.Create("fixture", [tree], ProtocolBoundaryChecker.RuntimeReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error));
        return new ProtocolBoundaryChecker(Shapes).Check(new("fixture.cs", role, tree), compilation.GetSemanticModel(tree));
    }
}
