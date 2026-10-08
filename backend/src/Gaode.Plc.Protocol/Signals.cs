using System.Collections.Frozen;
namespace Gaode.Plc.Protocol;

public enum PlcArea { Coil, HoldingRegister }

public enum SignalId
{
    PlcHeartbeatReq = 0,
    PcHeartbeatResp = 1,
    PcSystemReady = 2,
    PlcSystemFault = 3,
    PlcModeAuto = 4,
    SoftStopCmd = 5,
    SystemResetCmd = 6,
    PlcReadyState = 7,
    ManualZoneOccupied = 9,
    TeachModeCmd = 11,
    TeachConfirm = 12,
    CameraTargetX = 15,
    CameraTargetY = 16,
    CameraTargetZ = 17,
    ScanTargetZ = 18,
    GrabTargetZ = 19,
    MachineCurrentPosX = 20,
    MachineCurrentPosY = 21,
    MachineCurrentPosZ = 22,
    FlipTargetFace = 24,
    FlipStatus = 25,
    SortingCmd = 28,
    SortingExecStatus = 29,
    TeachPosSelect = 37,
    TeachPosX = 38,
    TeachPosY = 39,
    TeachPosZ = 40,
    AlarmBits = 42,
    AlarmSeverity = 43,
    FlipSorting = 48,
    FlipUnloadStatus = 49,
    ModelPayload = 50,
    XMoveStart = 51,
    YMoveStart = 52,
    ZCameraMoveStart = 53,
    ZScanMoveStart = 54,
    ZGrabMoveStart = 55,
    XPosConfirmed = 56,
    YPosConfirmed = 57,
    ZCameraPosConfirmed = 58,
    ZScanPosConfirmed = 59,
    ZGrapPosConfirmed = 60,
    ScanCurrentPosZ = 61,
    FlipGrapCurrentPosZ = 62,
    GrabId = 64,
    GrabActiveId = 65,
    RotateStart = 66,
    RotateTargetR = 67,
    RPosConfirmed = 68,
    MachineCurrentPosR = 69,
    ModelNumber = 70,
    EStopActive = 71,
    XAxisAlarm = 72,
    YAxisAlarm = 73,
    ZCameraAxisAlarm = 74,
    ZScanAxisAlarm = 75,
    ZFlipAxisAlarm = 76,
    RotateAxisAlarm = 77,
    FlipAxisAlarm = 78,
    PcCommunicationAlarm = 79,
    PcAlarm = 80,
    PcStartCmd = 81,
}

public enum PlcDirection { PcToPlc, PlcToPc }
public enum PlcWriter { Pc, Plc }
public enum PlcValueType { Bool, BoolWord, Int16, Float32, Words, BoolByte }

public sealed record PlcPoint(SignalId Id, int DocumentNumber, string DocumentAddress, string Name,
    PlcDirection Direction, PlcValueType ValueType, int RegisterCount = 1)
{
    public PlcArea Area { get; init; }
    public bool HostReadable { get; init; } = true;
    public PlcWriter Writer { get; init; }
    public PlcWriter ClearWriter { get; init; }
    public int ByteOffset { get; init; }
}

/// <summary>Explicit Test address map only. Confirmed field memory addresses live in ConfirmedMemoryLayout.</summary>
public static class PlcAddressMap
{
    public const string Contract = "plc-interaction-011-test-map";
    public const string SourceSha256 = "B0C30492E31A5C285A7E7397B69F0EBEFF39CC088F14174A38C638C9436D0E51";
    public const string RotationSourceSha256 = "0F8E404B0C8A1CA955A96ADB6A712CBED7AFCEFE72AF6AD11172F9BA8FA15547";
    public static int ToPduOffset(int documentNumber) => documentNumber is >= 1 and <= 65536 ? documentNumber - 1 : throw new ArgumentOutOfRangeException(nameof(documentNumber));

    public static class Coils
    {
        public const int PlcHeartbeatReq = 0x0001;
        public const int PcHeartbeatResp = 0x0002;
        public const int PcSystemReady = 0x0003;
        public const int PlcSystemFault = 0x0004;
        public const int PlcModeAuto = 0x0005;
        public const int SoftStopCmd = 0x0006;
        public const int SystemResetCmd = 0x0007;
        public const int PlcReadyState = 0x0008;
        public const int ManualZoneOccupied = 0x0010;
        public const int TeachModeCmd = 0x0020;
        public const int TeachConfirm = 0x0021;
        public const int XMoveStart = 0x0022;
        public const int YMoveStart = 0x0023;
        public const int ZCameraMoveStart = 0x0024;
        public const int ZScanMoveStart = 0x0025;
        public const int ZGrabMoveStart = 0x0026;
        public const int RotateStart = 0x0027; // Explicit 014 software map, not a formal PLC address.
    }
    public static class HoldingRegisters
    {
        public const int CameraTargetX = 0x0003;
        public const int CameraTargetY = 0x0005;
        public const int CameraTargetZ = 0x0007;
        public const int ScanTargetZ = 0x0009;
        public const int GrabTargetZ = 0x000B;
        public const int MachineCurrentPosX = 0x000D;
        public const int MachineCurrentPosY = 0x000F;
        public const int MachineCurrentPosZ = 0x0011;
        public const int FlipTargetFace = 0x0015;
        public const int FlipStatus = 0x0016;
        public const int SortingCmd = 0x0021;
        public const int SortingExecStatus = 0x0022;
        public const int TeachPosSelect = 0x0030;
        public const int TeachPosX = 0x0031;
        public const int TeachPosY = 0x0033;
        public const int TeachPosZ = 0x0035;
        public const int AlarmBits = 0x0050;
        public const int AlarmSeverity = 0x0051;
        public const int Reserved0056 = 0x0056;
        public const int XPosConfirmed = 0x0080;
        public const int YPosConfirmed = 0x0081;
        public const int ZCameraPosConfirmed = 0x0082;
        public const int ZScanPosConfirmed = 0x0083;
        public const int ZGrapPosConfirmed = 0x0084;
        public const int ScanCurrentPosZ = 0x0085;
        public const int FlipGrapCurrentPosZ = 0x0087;
        // Virtual placement is retained for existing tests; the confirmed field layout is separate.
        public const int GrabId = 0x0090;
        public const int GrabActiveId = 0x0091;
        public const int RotateTargetR = 0x0092;
        public const int RPosConfirmed = 0x0094;
        public const int MachineCurrentPosR = 0x0095;
    }

    private static PlcPoint C(SignalId id, int n, string name, PlcDirection d) =>
        new(id, n, $"0x{n:X4}", name, d, PlcValueType.Bool, 1)
        { Area = PlcArea.Coil, Writer = d == PlcDirection.PcToPlc ? PlcWriter.Pc : PlcWriter.Plc,
           ClearWriter = d == PlcDirection.PcToPlc ? PlcWriter.Pc : PlcWriter.Plc };
    private static PlcPoint R(SignalId id, int n, string name, PlcDirection d) =>
        new(id, n, $"4x{n:X4}", name, d, PlcValueType.Int16, 1)
        { Area = PlcArea.HoldingRegister, Writer = d == PlcDirection.PcToPlc ? PlcWriter.Pc : PlcWriter.Plc,
           ClearWriter = d == PlcDirection.PcToPlc ? PlcWriter.Pc : PlcWriter.Plc };
    private static PlcPoint B(SignalId id, int n, string name, PlcDirection d) =>
        new(id, n, $"4x{n:X4}", name, d, PlcValueType.BoolWord, 1)
        { Area = PlcArea.HoldingRegister, Writer = d == PlcDirection.PcToPlc ? PlcWriter.Pc : PlcWriter.Plc,
           ClearWriter = d == PlcDirection.PcToPlc ? PlcWriter.Pc : PlcWriter.Plc };
    private static PlcPoint F(SignalId id, int n, string name, PlcDirection d) =>
        new(id, n, $"4x{n:X4}", name, d, PlcValueType.Float32, 2)
        { Area = PlcArea.HoldingRegister, Writer = d == PlcDirection.PcToPlc ? PlcWriter.Pc : PlcWriter.Plc,
           ClearWriter = d == PlcDirection.PcToPlc ? PlcWriter.Pc : PlcWriter.Plc };

    public static readonly IReadOnlyDictionary<int, PlcPoint> CoilPoints = new Dictionary<int, PlcPoint>
    {
        [Coils.PlcHeartbeatReq] = C(SignalId.PlcHeartbeatReq, Coils.PlcHeartbeatReq, "PLC_Heartbeat_Req", PlcDirection.PlcToPc),
        [Coils.PcHeartbeatResp] = C(SignalId.PcHeartbeatResp, Coils.PcHeartbeatResp, "PC_Heartbeat_Resp", PlcDirection.PcToPlc),
        [Coils.PcSystemReady] = C(SignalId.PcSystemReady, Coils.PcSystemReady, "PC_System_Ready", PlcDirection.PcToPlc),
        [Coils.PlcSystemFault] = C(SignalId.PlcSystemFault, Coils.PlcSystemFault, "PLC_System_Fault", PlcDirection.PlcToPc),
        [Coils.PlcModeAuto] = C(SignalId.PlcModeAuto, Coils.PlcModeAuto, "PLC_Mode_Auto", PlcDirection.PlcToPc),
        [Coils.SoftStopCmd] = C(SignalId.SoftStopCmd, Coils.SoftStopCmd, "Soft_Stop_Cmd", PlcDirection.PcToPlc),
        [Coils.SystemResetCmd] = C(SignalId.SystemResetCmd, Coils.SystemResetCmd, "System_Reset_Cmd", PlcDirection.PcToPlc),
        [Coils.PlcReadyState] = C(SignalId.PlcReadyState, Coils.PlcReadyState, "PLC_Ready_State", PlcDirection.PlcToPc),
        [Coils.ManualZoneOccupied] = C(SignalId.ManualZoneOccupied, Coils.ManualZoneOccupied, "Manual_Zone_Occupied", PlcDirection.PlcToPc),
        [Coils.TeachModeCmd] = C(SignalId.TeachModeCmd, Coils.TeachModeCmd, "Teach_Mode_Cmd", PlcDirection.PcToPlc),
        [Coils.TeachConfirm] = C(SignalId.TeachConfirm, Coils.TeachConfirm, "Teach_Confirm", PlcDirection.PcToPlc),
        [Coils.XMoveStart] = C(SignalId.XMoveStart, Coils.XMoveStart, "X_Move_Start", PlcDirection.PcToPlc),
        [Coils.YMoveStart] = C(SignalId.YMoveStart, Coils.YMoveStart, "Y_Move_Start", PlcDirection.PcToPlc),
        [Coils.ZCameraMoveStart] = C(SignalId.ZCameraMoveStart, Coils.ZCameraMoveStart, "Z_Camera_Move_Start", PlcDirection.PcToPlc),
        [Coils.ZScanMoveStart] = C(SignalId.ZScanMoveStart, Coils.ZScanMoveStart, "Z_Scan_Move_Start", PlcDirection.PcToPlc),
        [Coils.ZGrabMoveStart] = C(SignalId.ZGrabMoveStart, Coils.ZGrabMoveStart, "Z_Grab_Move_Start", PlcDirection.PcToPlc),
        [Coils.RotateStart] = C(SignalId.RotateStart, Coils.RotateStart, "Rotate_Start", PlcDirection.PcToPlc),
    }.ToFrozenDictionary();
    public static readonly IReadOnlyDictionary<int, PlcPoint> HoldingRegisterPoints = new Dictionary<int, PlcPoint>
    {
        [HoldingRegisters.CameraTargetX] = F(SignalId.CameraTargetX, HoldingRegisters.CameraTargetX, "Camera_Target_X", PlcDirection.PcToPlc),
        [HoldingRegisters.CameraTargetY] = F(SignalId.CameraTargetY, HoldingRegisters.CameraTargetY, "Camera_Target_Y", PlcDirection.PcToPlc),
        [HoldingRegisters.CameraTargetZ] = F(SignalId.CameraTargetZ, HoldingRegisters.CameraTargetZ, "Camera_Target_Z", PlcDirection.PcToPlc),
        [HoldingRegisters.ScanTargetZ] = F(SignalId.ScanTargetZ, HoldingRegisters.ScanTargetZ, "Scan_Target_Z", PlcDirection.PcToPlc),
        [HoldingRegisters.GrabTargetZ] = F(SignalId.GrabTargetZ, HoldingRegisters.GrabTargetZ, "Grab_Target_Z", PlcDirection.PcToPlc),
        [HoldingRegisters.MachineCurrentPosX] = F(SignalId.MachineCurrentPosX, HoldingRegisters.MachineCurrentPosX, "Machine_Current_Pos_X", PlcDirection.PlcToPc),
        [HoldingRegisters.MachineCurrentPosY] = F(SignalId.MachineCurrentPosY, HoldingRegisters.MachineCurrentPosY, "Machine_Current_Pos_Y", PlcDirection.PlcToPc),
        [HoldingRegisters.MachineCurrentPosZ] = F(SignalId.MachineCurrentPosZ, HoldingRegisters.MachineCurrentPosZ, "Machine_Current_Pos_Z", PlcDirection.PlcToPc),
        [HoldingRegisters.FlipTargetFace] = R(SignalId.FlipTargetFace, HoldingRegisters.FlipTargetFace, "Flip_Target_Face", PlcDirection.PcToPlc),
        [HoldingRegisters.FlipStatus] = R(SignalId.FlipStatus, HoldingRegisters.FlipStatus, "Flip_Status", PlcDirection.PlcToPc),
        [HoldingRegisters.SortingCmd] = R(SignalId.SortingCmd, HoldingRegisters.SortingCmd, "Sorting_Cmd", PlcDirection.PcToPlc),
        [HoldingRegisters.SortingExecStatus] = R(SignalId.SortingExecStatus, HoldingRegisters.SortingExecStatus, "Sorting_Exec_Status", PlcDirection.PlcToPc),
        [HoldingRegisters.TeachPosSelect] = R(SignalId.TeachPosSelect, HoldingRegisters.TeachPosSelect, "Teach_Pos_Select", PlcDirection.PcToPlc),
        [HoldingRegisters.TeachPosX] = F(SignalId.TeachPosX, HoldingRegisters.TeachPosX, "Teach_Pos_X", PlcDirection.PlcToPc),
        [HoldingRegisters.TeachPosY] = F(SignalId.TeachPosY, HoldingRegisters.TeachPosY, "Teach_Pos_Y", PlcDirection.PlcToPc),
        [HoldingRegisters.TeachPosZ] = F(SignalId.TeachPosZ, HoldingRegisters.TeachPosZ, "Teach_Pos_Z", PlcDirection.PlcToPc),
        [HoldingRegisters.AlarmBits] = R(SignalId.AlarmBits, HoldingRegisters.AlarmBits, "Alarm_Bits", PlcDirection.PlcToPc),
        [HoldingRegisters.AlarmSeverity] = R(SignalId.AlarmSeverity, HoldingRegisters.AlarmSeverity, "Alarm_Severity", PlcDirection.PlcToPc),
        [HoldingRegisters.XPosConfirmed] = R(SignalId.XPosConfirmed, HoldingRegisters.XPosConfirmed, "X_Pos_Confirmed", PlcDirection.PlcToPc),
        [HoldingRegisters.YPosConfirmed] = R(SignalId.YPosConfirmed, HoldingRegisters.YPosConfirmed, "Y_Pos_Confirmed", PlcDirection.PlcToPc),
        [HoldingRegisters.ZCameraPosConfirmed] = R(SignalId.ZCameraPosConfirmed, HoldingRegisters.ZCameraPosConfirmed, "Z_Camera_Pos_Confirmed", PlcDirection.PlcToPc),
        [HoldingRegisters.ZScanPosConfirmed] = R(SignalId.ZScanPosConfirmed, HoldingRegisters.ZScanPosConfirmed, "Z_Scan_Pos_Confirmed", PlcDirection.PlcToPc),
        [HoldingRegisters.ZGrapPosConfirmed] = R(SignalId.ZGrapPosConfirmed, HoldingRegisters.ZGrapPosConfirmed, "Z_Grap_Pos_Confirmed", PlcDirection.PlcToPc),
        [HoldingRegisters.ScanCurrentPosZ] = F(SignalId.ScanCurrentPosZ, HoldingRegisters.ScanCurrentPosZ, "Scan_Current_Pos_Z", PlcDirection.PlcToPc),
        [HoldingRegisters.FlipGrapCurrentPosZ] = F(SignalId.FlipGrapCurrentPosZ, HoldingRegisters.FlipGrapCurrentPosZ, "Flip_Grap_Current_Pos_Z", PlcDirection.PlcToPc),
        [HoldingRegisters.GrabId] = R(SignalId.GrabId, HoldingRegisters.GrabId, "Grab_ID", PlcDirection.PcToPlc),
        [HoldingRegisters.GrabActiveId] = R(SignalId.GrabActiveId, HoldingRegisters.GrabActiveId, "Grab_Active_ID", PlcDirection.PlcToPc),
        [HoldingRegisters.RotateTargetR] = F(SignalId.RotateTargetR, HoldingRegisters.RotateTargetR, "Rotate_Target_R", PlcDirection.PcToPlc),
        [HoldingRegisters.RPosConfirmed] = R(SignalId.RPosConfirmed, HoldingRegisters.RPosConfirmed, "R_Pos_Confirmed", PlcDirection.PlcToPc),
        [HoldingRegisters.MachineCurrentPosR] = F(SignalId.MachineCurrentPosR, HoldingRegisters.MachineCurrentPosR, "Machine_Current_Pos_R", PlcDirection.PlcToPc),
        // Explicit Test placement only; field Model_Number still has an unresolved encoding contract.
        [0x0062] = new(SignalId.ModelPayload, 0x0062, "Test:0x0062", "Model_Payload_Test_Only", PlcDirection.PcToPlc, PlcValueType.Words, 16)
            { Area = PlcArea.HoldingRegister, Writer = PlcWriter.Pc, ClearWriter = PlcWriter.Pc },
        [0x0060] = R(SignalId.FlipSorting, 0x0060, "Flip_Sorting", PlcDirection.PcToPlc),
        [0x0061] = R(SignalId.FlipUnloadStatus, 0x0061, "Flip_Unload_Status", PlcDirection.PlcToPc),
    }.ToFrozenDictionary();

    public static PlcPoint? RegisterAt(int documentNumber) => HoldingRegisterPoints.Values
        .FirstOrDefault(p => documentNumber >= p.DocumentNumber &&
            documentNumber < p.DocumentNumber + p.RegisterCount);
}
