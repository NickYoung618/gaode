#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Config = '',
    [ValidateRange(1,86400)][int]$DurationSeconds = 60,
    [switch]$Heartbeat,
    [switch]$AllowMotion,
    [string]$ActionName = '',
    [double]$Target = [double]::NaN,
    [switch]$NoDashboard,
    [string]$OutputDirectory = ''
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $Config) { $Config = Join-Path $PSScriptRoot 'site.json' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot 'runs' }
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)

function File-Sha256([string]$Path) {
    $hash=[Security.Cryptography.SHA256]::Create(); $inputFile=[IO.File]::OpenRead($Path)
    try { return [BitConverter]::ToString($hash.ComputeHash($inputFile)).Replace('-','') }
    finally { $inputFile.Dispose(); $hash.Dispose() }
}

function Integer($Value, [int]$Min, [int]$Max, [string]$Name) {
    $n = 0
    if ($null -eq $Value -or $Value -is [bool] -or
        -not [int]::TryParse([string]$Value, [ref]$n) -or $n -lt $Min -or $n -gt $Max) {
        throw "配置 $Name 必须填写十进制整数，范围 $Min..$Max；不能使用空地址或区前缀。"
    }
    return $n
}
function Validate-Config($c) {
    if ($c.schemaVersion -ne 1 -or $c.purpose -notin @('Field','Virtual')) { throw '配置版本或用途无效。' }
    if ([string]::IsNullOrWhiteSpace($c.sourceReference)) { throw '请填写点表来源 sourceReference。' }
    $ip = $null
    if (-not [System.Net.IPAddress]::TryParse([string]$c.host, [ref]$ip)) { throw '请填写现场 PLC 的 IP（host）。' }
    if ($c.purpose -eq 'Virtual' -and -not [System.Net.IPAddress]::IsLoopback($ip)) {
        throw 'Virtual 测试点表仅允许连接本机回环地址，不能用于现场 PLC。'
    }
    $c.port = Integer $c.port 1 65535 'port'
    $c.unitId = Integer $c.unitId 1 255 'unitId'
    $c.addressBase = Integer $c.addressBase 0 1 'addressBase'
    $c.intervalMs = Integer $c.intervalMs 100 10000 'intervalMs'
    $c.timeoutMs = Integer $c.timeoutMs 100 3000 'timeoutMs'
    $active = @($c.signals | Where-Object { $_.enabled -eq $true })
    if ($active.Count -eq 0) { throw '至少启用一个已确认地址的信号（enabled=true）。' }
    $names = @{}; $occupied = @{}
    foreach ($p in $active) {
        if ([string]::IsNullOrWhiteSpace($p.name) -or $names.ContainsKey($p.name)) { throw '信号名称为空或重复。' }
        $names[$p.name] = $p
        if ($p.area -notin @('Coil','DiscreteInput','HoldingRegister','InputRegister')) { throw "未知数据区：$($p.name)" }
        if ($p.type -notin @('Bool','UInt16','Int16','Float32')) { throw "未知类型：$($p.name)" }
        if (($p.area -in @('Coil','DiscreteInput')) -ne ($p.type -eq 'Bool')) { throw "类型与数据区不一致：$($p.name)" }
        if ($p.direction -notin @('PLC->PC','PC->PLC')) { throw "信号方向未确认：$($p.name)" }
        if ($p.type -eq 'Float32' -and $p.byteOrder -notin @('ABCD','CDAB','BADC','DCBA')) { throw "浮点字序未确认：$($p.name)" }
        $width = 1; if ($p.type -eq 'Float32') { $width = 2 }
        $address = Integer $p.address $c.addressBase (65536 - $width + $c.addressBase) $p.name
        $offset = $address - $c.addressBase
        $p | Add-Member NoteProperty offset $offset
        $p | Add-Member NoteProperty width $width
        for ($i=0; $i -lt $width; $i++) {
            $key = "$($p.area):$($offset + $i)"
            if ($occupied.ContainsKey($key)) { throw "点表地址重叠：$($p.name) / $($occupied[$key])" }
            $occupied[$key] = $p.name
        }
    }
    if ($AllowMotion -and -not $Heartbeat) { throw '动作测试必须同时启用-Heartbeat，持续应答PLC心跳。' }
    if ($ActionName -and (-not $AllowMotion -or [double]::IsNaN($Target) -or [double]::IsInfinity($Target))) { throw '自动单步需要-AllowMotion、-ActionName和有限数值-Target。' }
    if ($AllowMotion) {
        if ($c.motion.confirmed -ne $true -or [string]::IsNullOrWhiteSpace($c.motion.sourceReference)) {
            throw '动作未确认：需现场核对轴含义、动作点、允许范围、到位容差和PLC联锁后填写motion。'
        }
        foreach ($guard in @('PLC_Ready_State','PLC_Mode_Auto','PLC_System_Fault')) {
            if (-not $names.ContainsKey($guard) -or $names[$guard].type -ne 'Bool' -or $names[$guard].direction -ne 'PLC->PC') { throw "动作缺少状态观察：$guard" }
        }
        if (@($c.motion.actions).Count -eq 0) { throw '未配置可测试的轴。' }
        $axisNames=@{}
        foreach ($a in $c.motion.actions) {
            if ([string]::IsNullOrWhiteSpace($a.name) -or $axisNames.ContainsKey($a.name)) { throw '动作名称为空或重复。' }
            $axisNames[$a.name]=$true
            foreach ($binding in @(@('targetSignal','Float32','PC->PLC','HoldingRegister'),@('startSignal','Bool','PC->PLC','Coil'),@('feedbackSignal','Int16','PLC->PC','HoldingRegister'),@('actualSignal','Float32','PLC->PC','HoldingRegister'))) {
                $name=$a.($binding[0])
                if (-not $names.ContainsKey($name) -or $names[$name].type -ne $binding[1] -or $names[$name].direction -ne $binding[2] -or $names[$name].area -ne $binding[3]) { throw "动作 $($a.name) 的 $($binding[0]) 未确认或类型/方向错误。" }
            }
            # Bind only source-defined axis quadruples; arbitrary control bits are not motion starts.
            $known = @{
                X=@('Camera_Target_X','X_Move_Start','X_Pos_Confirmed','Machine_Current_Pos_X')
                Y=@('Camera_Target_Y','Y_Move_Start','Y_Pos_Confirmed','Machine_Current_Pos_Y')
                CameraZ=@('Camera_Target_Z','Z_Camera_Move_Start','Z_Camera_Pos_Confirmed','Machine_Current_Pos_Z')
                ScanZ=@('Scan_Target_Z','Z_Scan_Move_Start','Z_Scan_Pos_Confirmed','Scan_Current_Pos_Z')
                GrabZ=@('Grab_Target_Z','Z_Grab_Move_Start','Z_Grap_Pos_Confirmed','Flip_Grap_Current_Pos_Z')
                R=@('Rotate_Target_R','Rotate_Start','R_Pos_Confirmed','Machine_Current_Pos_R')
            }
            if (-not $known.ContainsKey($a.name) -or (@($a.targetSignal,$a.startSignal,$a.feedbackSignal,$a.actualSignal) -join '|') -ne ($known[$a.name] -join '|')) { throw "不支持的轴映射：$($a.name)" }
            foreach ($field in @('min','max','tolerance')) {
                if ($null -eq $a.$field -or $a.$field -is [bool] -or $a.$field -is [string] -or [double]::IsNaN([double]$a.$field) -or [double]::IsInfinity([double]$a.$field)) { throw "动作 $($a.name) 缺少有限数值 $field。" }
            }
            if ($a.min -ge $a.max -or $a.tolerance -le 0 -or [string]::IsNullOrWhiteSpace($a.unit) -or [string]::IsNullOrWhiteSpace($a.frame)) { throw "动作 $($a.name) 范围/容差/单位/坐标系未确认。" }
            $a.timeoutMs=Integer $a.timeoutMs 100 120000 "$($a.name).timeoutMs"
        }
        if ($ActionName -and -not $axisNames.ContainsKey($ActionName)) { throw "未配置动作：$ActionName" }
    }
    if ($Heartbeat) {
        $h = $c.heartbeat
        if ($h.confirmed -ne $true -or $h.mode -ne 'EchoOnChange' -or
            $h.requestSignal -ne 'PLC_Heartbeat_Req' -or $h.responseSignal -ne 'PC_Heartbeat_Resp') {
            throw '心跳写入未确认：请先由现场核对点位和同值应答，再设置 heartbeat.confirmed=true。'
        }
        foreach ($name in @($h.requestSignal,$h.responseSignal)) {
            if (-not $names.ContainsKey($name) -or $names[$name].area -ne 'Coil' -or $names[$name].type -ne 'Bool') {
                throw "心跳需要已启用的 Bool Coil 点：$name"
            }
        }
        if ($names[$h.requestSignal].direction -ne 'PLC->PC' -or $names[$h.responseSignal].direction -ne 'PC->PLC') {
            throw '心跳方向错误：请求由PLC产生，应答由PC写入。'
        }
    }
    return ,$active
}
function Event([string]$Level, [string]$Category, [string]$Message, $Data = $null) {
    $entry = [ordered]@{ atUtc=[DateTimeOffset]::UtcNow.ToString('o'); sessionId=$script:sessionId
        level=$Level; category=$Category; message=$Message; data=$Data }
    $script:eventLog.WriteLine(($entry | ConvertTo-Json -Compress -Depth 12))
    $line = '{0:HH:mm:ss.fff} [{1}] {2}' -f [DateTime]::Now,$Category,$Message
    $script:recent.Enqueue($line)
    while ($script:recent.Count -gt 8) { [void]$script:recent.Dequeue() }
    if (-not $script:dashboard) { Write-Host $line }
}
function Wire([string]$Direction, [byte[]]$Bytes, [string]$ErrorText = '') {
    $entry = [ordered]@{ atUtc=[DateTimeOffset]::UtcNow.ToString('o'); sessionId=$script:sessionId
        sequence=$script:sequence; transactionId=$script:transaction; direction=$Direction
        context=$script:wireContext; hex=[BitConverter]::ToString($Bytes).Replace('-',' ')
        elapsedMs=[math]::Round($script:ioWatch.Elapsed.TotalMilliseconds,2); error=$ErrorText }
    $script:wireLog.WriteLine(($entry | ConvertTo-Json -Compress -Depth 8))
}
function Read-Exact([int]$Count) {
    $buffer = New-Object byte[] $Count
    $done = 0
    while ($done -lt $Count) {
        $remaining = $script:c.timeoutMs - [int]$script:ioWatch.ElapsedMilliseconds
        if ($remaining -le 0) { throw 'Modbus响应超时。' }
        $script:stream.ReadTimeout = [math]::Max(1,$remaining)
        $n = $script:stream.Read($buffer,$done,$Count-$done)
        if ($n -eq 0) { throw 'PLC已关闭TCP连接。' }
        for ($j=0; $j -lt $n; $j++) { $script:received.Add($buffer[$done+$j]) }
        $done += $n
    }
    return ,$buffer
}
function Word([byte[]]$Bytes, [int]$Index) { return ([int]$Bytes[$Index] * 256 + [int]$Bytes[$Index+1]) }
function Exchange([int]$Function, [int]$Offset, [int]$Argument, [string]$Context, [byte[]]$Payload = @()) {
    # Writes are confined to the heartbeat and the currently admitted, source-defined single axis.
    if ($Function -in @(5,16)) {
        $allowed = $Function -eq 5 -and $Heartbeat -and $Offset -eq $script:responsePoint.offset -and $Argument -in @(0,65280)
        if ($AllowMotion -and $null -ne $script:action) {
            $a=$script:action.definition
            if ($Function -eq 5 -and $Offset -eq $script:byName[$a.startSignal].offset -and $Argument -in @(0,65280)) { $allowed=$true }
            if ($Function -eq 16 -and $Offset -eq $script:byName[$a.targetSignal].offset -and $Argument -eq 2 -and $Payload.Length -eq 4) { $allowed=$true }
        }
        if (-not $allowed) { throw '拒绝未受理的写入。' }
        $script:stats.writeAttempts++
    } elseif ($Function -notin @(1,2,3,4)) { throw '不支持此功能码。' }
    $script:sequence++
    $script:transaction = ($script:transaction + 1) % 65536
    $script:wireContext = $Context
    [byte[]]$request = @(
        ($script:transaction -shr 8),($script:transaction -band 255),0,0,0,6,$script:c.unitId,
        $Function,($Offset -shr 8),($Offset -band 255),($Argument -shr 8),($Argument -band 255))
    if ($Function -eq 16) { $request += [byte]4; $request += $Payload; $request[5]=11 }
    $script:received = New-Object 'System.Collections.Generic.List[byte]'
    $script:ioWatch = [Diagnostics.Stopwatch]::StartNew()
    try {
        Wire 'TX' $request
        $script:stream.Write($request,0,$request.Length)
        $header = Read-Exact 7
        $length = Word $header 4
        if ((Word $header 0) -ne $script:transaction -or (Word $header 2) -ne 0 -or
            $header[6] -ne $script:c.unitId -or $length -lt 2 -or $length -gt 254) {
            throw 'Modbus MBAP不匹配（事务号/协议号/站号/长度），响应不可用于本次请求。'
        }
        $pdu = Read-Exact ($length-1)
        Wire 'RX' $script:received.ToArray()
        if ($pdu[0] -eq ($Function -bor 128)) {
            if ($pdu.Length -ne 2) { throw 'Modbus异常响应长度不正确。' }
            throw ('PLC返回Modbus异常码 0x{0:X2}（01=功能不支持，02=地址非法，03=值非法，04=设备失败）' -f $pdu[1])
        }
        if ($pdu[0] -ne $Function) { throw 'Modbus功能码不匹配。' }
        if ($Function -in @(5,16)) {
            if ($pdu.Length -ne 5 -or (Word $pdu 1) -ne $Offset -or (Word $pdu 3) -ne $Argument) {
                throw '写入回显不匹配；写入结果未知，不自动重试。'
            }
            $script:stats.writeResponses++
        } else {
            $byteCount = $Argument*2
            if ($Function -in @(1,2)) { $byteCount = [int][math]::Ceiling($Argument/8.0) }
            if ($pdu.Length -ne (2+$byteCount) -or $pdu[1] -ne $byteCount) { throw 'Modbus读取字节数与请求不一致。' }
            $script:stats.readResponses++
        }
        $elapsed = $script:ioWatch.Elapsed.TotalMilliseconds
        $script:stats.responseCount++
        $script:stats.totalResponseMs += $elapsed
        $script:stats.maxResponseMs = [math]::Max($script:stats.maxResponseMs,$elapsed)
        return ,$pdu
    } catch {
        Wire 'ERROR' $script:received.ToArray() $_.Exception.Message
        throw "请求 $($script:sequence) [$Context] FC=$Function offset=$Offset : $($_.Exception.Message)"
    }
}
function Read-Group($Group) {
    $fc = @{Coil=1; DiscreteInput=2; HoldingRegister=3; InputRegister=4}[$Group.area]
    $pdu = Exchange $fc $Group.offset $Group.count ($Group.points.name -join ',')
    $sampleUtc = [DateTimeOffset]::UtcNow.ToString('o')
    foreach ($p in $Group.points) {
        $index = $p.offset - $Group.offset
        if ($p.type -eq 'Bool') {
            $value = [int](($pdu[2+[int][math]::Floor($index/8)] -band (1 -shl ($index%8))) -ne 0)
            $raw = [string]$value
        } else {
            $index = 2+$index*2
            $word = Word $pdu $index
            $raw = '{0:X4}' -f $word
            $value = $word
            if ($p.type -eq 'Int16' -and $word -gt 32767) { $value = $word-65536 }
            if ($p.type -eq 'Float32') {
                $raw += ' {0:X4}' -f (Word $pdu ($index+2))
                [byte[]]$bytes = $pdu[$index..($index+3)]
                $order = @{ABCD=@(0,1,2,3); CDAB=@(2,3,0,1); BADC=@(1,0,3,2); DCBA=@(3,2,1,0)}[$p.byteOrder]
                [byte[]]$canonical = $bytes[$order]
                if ([BitConverter]::IsLittleEndian) { [array]::Reverse($canonical) }
                $value = [BitConverter]::ToSingle($canonical,0)
                # Keep invalid float visible, without presenting it as a usable coordinate.
                if ([single]::IsNaN($value) -or [single]::IsInfinity($value)) { $value = "无效Float32($value)" }
            }
        }
        $previous = $script:values[$p.name]
        if ($null -eq $previous -or $previous.raw -ne $raw) {
            $old = '--'; if ($null -ne $previous) { $old = $previous.value; $script:stats.changes++ }
            Event 'Info' 'Signal' "$($p.direction) $($p.name) $old -> $value [原始:$raw]" @{
                signal=$p.name; label=$p.label; area=$p.area; offset=$p.offset; raw=$raw
                oldValue=$old; value=$value; requestSequence=$script:sequence; sampleUtc=$sampleUtc }
        }
        $script:values[$p.name] = @{value=$value; raw=$raw; sampleUtc=$sampleUtc; sequence=$script:sequence}
    }
}
function Groups($Points) {
    $groups = New-Object System.Collections.ArrayList
    foreach ($p in ($Points | Sort-Object area,offset)) {
        $limit = 125; if ($p.type -eq 'Bool') { $limit = 2000 }
        $last = $null; if ($groups.Count -gt 0) { $last = $groups[$groups.Count-1] }
        if ($null -ne $last -and $last.area -eq $p.area -and ($last.offset+$last.count) -eq $p.offset -and ($last.count+$p.width) -le $limit) {
            $last.count += $p.width; $last.points += @($p)
        } else { [void]$groups.Add(@{area=$p.area; offset=$p.offset; count=$p.width; points=@($p)}) }
    }
    return ,$groups
}
function Show-Board([string]$Status) {
    if (-not $script:dashboard) { return }
    [Console]::Clear()
    Write-Host '高德 PLC 通信探针 0.2.0 | Q结束（动作执行时须先现场处置）' -ForegroundColor Cyan
    Write-Host "$($script:c.purpose)  $($script:c.host):$($script:c.port)  unit=$($script:c.unitId)  $script:mode  状态=$Status"
    Write-Host "点表来源: $($script:c.sourceReference)"
    Write-Host "地址基准=$($script:c.addressBase)  轮询=$($script:c.intervalMs)ms  输出=$script:runDir"
    Write-Host ('读响应={0} 写响应={1} 心跳变化={2} 读回匹配={3} 最大响应={4:N1}ms' -f
        $script:stats.readResponses,$script:stats.writeResponses,$script:stats.heartbeatEdges,$script:stats.heartbeatReadbacks,$script:stats.maxResponseMs)
    Write-Host '方向     信号 / 中文说明                           区 / PDU偏移   原始值       当前值 / 采样时间'
    foreach ($p in $script:points) {
        $v = $script:values[$p.name]
        if ($null -eq $v) { Write-Host "$($p.direction) $($p.name) 等待读取"; continue }
        Write-Host ('{0,-7} {1} ({2}) | {3}/{4} | {5} | {6} @ {7}' -f
            $p.direction,$p.name,$p.label,$p.area,$p.offset,$v.raw,$v.value,([DateTimeOffset]::Parse($v.sampleUtc).ToLocalTime().ToString('HH:mm:ss.fff')))
    }
    Write-Host '最近交互（完整报文见 wire.jsonl；信号变化见 events.jsonl）' -ForegroundColor Cyan
    foreach ($line in $script:recent) { Write-Host $line }
    if ($AllowMotion) {
        Write-Host '单步命令：move X 12.5（轴名按配置）；quit结束。一次只执行一个轴。' -ForegroundColor Yellow
        Write-Host "> $script:commandBuffer"
    }
    if ($Status -ne '采集中') { Write-Host '停止后的表值仅为最后样本，不代表PLC当前状态。' -ForegroundColor Yellow }
}

function Motion-Guards {
    if (($script:watch.ElapsedMilliseconds-$script:lastEdgeMs) -ge 3000) { throw '动作准入时PLC心跳已过期。' }
    foreach ($g in @(@('PLC_Ready_State',1),@('PLC_Mode_Auto',1),@('PLC_System_Fault',0))) {
        if ($script:values[$g[0]].value -ne $g[1]) { throw "动作状态条件不成立：$($g[0]) 应为 $($g[1])。" }
    }
}
function Start-Motion([string]$Name, [double]$Position) {
    if ($null -ne $script:action) { throw '已有动作在执行，不能叠加。' }
    $found=@($script:c.motion.actions | Where-Object name -eq $Name)
    if ($found.Count -ne 1) { throw "未配置动作：$Name" }
    $a=$found[0]
    if ([double]::IsNaN($Position) -or [double]::IsInfinity($Position) -or $Position -lt $a.min -or $Position -gt $a.max) { throw "目标超出现场允许范围 [$($a.min), $($a.max)] $($a.unit)。" }
    Motion-Guards
    if ($script:stats.heartbeatEdges -lt 1) { throw '尚未观察到PLC心跳变化，不受理动作。' }
    if ($script:values[$a.startSignal].value -ne 0) { throw '启动信号已置位，不能接管未知动作或重复触发。' }
    $point=$script:byName[$a.targetSignal]
    $encoded=[single]$Position
    if ([single]::IsInfinity($encoded) -or [math]::Abs([double]$encoded-$Position) -gt $a.tolerance -or $encoded -lt $a.min -or $encoded -gt $a.max) { throw 'Float32编码不能在允许范围和容差内表达目标。' }
    [byte[]]$bytes=[BitConverter]::GetBytes($encoded)
    if ([BitConverter]::IsLittleEndian) { [array]::Reverse($bytes) }
    $order=@{ABCD=@(0,1,2,3);CDAB=@(2,3,0,1);BADC=@(1,0,3,2);DCBA=@(3,2,1,0)}[$point.byteOrder]
    $script:action=@{id=[guid]::NewGuid().ToString('N');definition=$a;target=[double]$encoded;phase='TargetWrite';sentMs=$script:watch.ElapsedMilliseconds}
    Event 'Info' 'ActionAccepted' "受理单轴 $Name -> $encoded $($a.unit)；坐标系=$($a.frame)" $script:action
    [void](Exchange 16 $point.offset 2 "$Name`:target" $bytes[$order])
    Event 'Info' 'TargetWritten' "$Name 目标已写入；尚未启动运动。" @{actionId=$script:action.id;requestSequence=$script:sequence}
    $script:action.phase='StartWrite'
    [void](Exchange 5 $script:byName[$a.startSignal].offset 65280 "$Name`:start")
    $script:action.phase='AwaitMoving'
    $script:action.sentSequence=$script:sequence
    Event 'Info' 'ActionStarted' "$Name 启动已响应；等待本次反馈0(运动中) -> 1(到位)，2为超时。" @{actionId=$script:action.id;requestSequence=$script:sequence}
}
function Advance-Motion {
    if ($null -eq $script:action) { return }
    $a=$script:action.definition
    Motion-Guards
    if (($script:watch.ElapsedMilliseconds-$script:action.sentMs) -ge $a.timeoutMs) { throw "动作 $($a.name) 超时，阶段=$($script:action.phase)；不自动重发/清零，须现场处置。" }
    $feedback=$script:values[$a.feedbackSignal]
    if ($feedback.sequence -le $script:action.sentSequence) { return }
    if ($feedback.value -eq 2) { throw "$($a.name) PLC反馈超时未到位(2)。" }
    if ($feedback.value -notin @(0,1)) { throw "$($a.name) PLC反馈未知码 $($feedback.value)。" }
    if ($script:action.phase -eq 'AwaitMoving' -and $feedback.value -eq 0) {
        $script:action.phase='AwaitArrived'
        Event 'Info' 'ActionMoving' "$($a.name) 已观察本次运动中，等待到位。" @{actionId=$script:action.id;requestSequence=$feedback.sequence}
    } elseif ($script:action.phase -eq 'AwaitArrived' -and $feedback.value -eq 1) {
        # Read actual position after arrival, so a split poll cannot use a pre-arrival position.
        $p=$script:byName[$a.actualSignal]
        Read-Group @{area=$p.area;offset=$p.offset;count=$p.width;points=@($p)}
        $actual=$script:values[$a.actualSignal].value
        if ($actual -is [string] -or [math]::Abs([double]$actual-$script:action.target) -gt $a.tolerance) { throw "$($a.name) 到位码=1，但实际位置 $actual 与目标不符。" }
        if (($script:watch.ElapsedMilliseconds-$script:action.sentMs) -ge $a.timeoutMs) { throw '到位核验已超过动作期限。' }
        $script:action.phase='ClearStart'
        [void](Exchange 5 $script:byName[$a.startSignal].offset 0 "$($a.name):clear-start")
        $p=$script:byName[$a.startSignal]
        Read-Group @{area=$p.area;offset=$p.offset;count=1;points=@($p)}
        if ($script:values[$a.startSignal].value -ne 0) { throw '启动清零读回不一致。' }
        $script:stats.actionsCompleted++
        Event 'Info' 'ActionCompleted' "$($a.name) 本次到位，实测=$actual，启动已清零读回。" @{actionId=$script:action.id;target=$script:action.target;actual=$actual;requestSequence=$script:sequence}
        $script:action=$null
    }
}

$exitCode = 0; $eventLog = $null; $wireLog = $null; $client = $null
try {
    if (-not (Test-Path -LiteralPath $Config)) { throw '缺少site.json：复制site.template.json为site.json，并填写现场连接参数及已确认点位。' }
    $configText = Get-Content -LiteralPath $Config -Raw -Encoding UTF8
    $c = $configText | ConvertFrom-Json
    $points = Validate-Config $c
    $byName=@{}; foreach ($p in $points) { $byName[$p.name]=$p }
    $groups = Groups $points
    $mode = '只读'; if ($Heartbeat) { $mode = '心跳同值应答（仅指定线圈）' }
    if ($AllowMotion) { $mode='现场确认的单轴动作+心跳' }
    $dashboard = -not $NoDashboard -and -not [Console]::IsOutputRedirected -and -not [Console]::IsInputRedirected
    $sessionId = [guid]::NewGuid().ToString('N')
    $runDir = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ((Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+$sessionId.Substring(0,8))
    [void][IO.Directory]::CreateDirectory($runDir)
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [IO.File]::WriteAllText((Join-Path $runDir 'config.snapshot.json'),$configText,$utf8)
    $eventLog = New-Object IO.StreamWriter((Join-Path $runDir 'events.jsonl'),$false,$utf8)
    $wireLog = New-Object IO.StreamWriter((Join-Path $runDir 'wire.jsonl'),$false,$utf8)
    $eventLog.AutoFlush = $true; $wireLog.AutoFlush = $true
    $recent = New-Object 'System.Collections.Generic.Queue[string]'
    $values = @{}; $sequence = 0; $transaction = 0; $status = 'Running'; $failure = $null
    $action=$null; $commandBuffer=''; $automaticActionSent=$false
    $stats = [ordered]@{ readResponses=0; writeAttempts=0; writeResponses=0; heartbeatEdges=0
        heartbeatReadbacks=0; actionsCompleted=0; changes=0; responseCount=0; totalResponseMs=0.0; maxResponseMs=0.0 }
    $startedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $requestPoint = @($points | Where-Object name -eq 'PLC_Heartbeat_Req')
    $responsePoint = $null
    if ($Heartbeat) { $responsePoint = @($points | Where-Object name -eq 'PC_Heartbeat_Resp')[0] }
    $heartbeatPrevious = $null; $lastEdgeMs = 0; $nextBoardMs = 0
    Event 'Info' 'Start' "受理诊断：$mode；仅证明本次读取/应答事实。" @{purpose=$c.purpose; source=$c.sourceReference; endpoint="$($c.host):$($c.port)"; unitId=$c.unitId}
    $client = New-Object Net.Sockets.TcpClient
    $client.NoDelay = $true
    $connect = $client.ConnectAsync($c.host,$c.port)
    if (-not $connect.Wait($c.timeoutMs)) { throw 'TCP连接超时，请检查IP、端口、网线及PLC服务。' }
    $stream = $client.GetStream(); $stream.WriteTimeout = $c.timeoutMs
    Event 'Info' 'Connected' 'TCP已连接；开始验证Modbus响应。' @{local=[string]$client.Client.LocalEndPoint; remote=[string]$client.Client.RemoteEndPoint}
    $watch.Restart()
    $lastEdgeMs = $watch.ElapsedMilliseconds
    while ($watch.Elapsed.TotalSeconds -lt $DurationSeconds) {
        $cycleStart = $watch.ElapsedMilliseconds
        foreach ($group in $groups) { Read-Group $group }
        if ($requestPoint.Count -eq 1) {
            $bit = $values['PLC_Heartbeat_Req'].value
            if (($watch.ElapsedMilliseconds-$lastEdgeMs) -ge 3000) { throw 'PLC心跳超过3000ms未观察到变化（HeartbeatStoppedChanging）。' }
            $changed = $null -eq $heartbeatPrevious -or $heartbeatPrevious -ne $bit
            if ($null -ne $heartbeatPrevious -and $heartbeatPrevious -ne $bit) {
                $stats.heartbeatEdges++; $lastEdgeMs = $watch.ElapsedMilliseconds
            }
            if ($Heartbeat -and $changed) {
                $rawBit = 0; if ($bit -eq 1) { $rawBit = 65280 }
                Event 'Info' 'HeartbeatTX' "PC -> PLC PC_Heartbeat_Resp=$bit" @{requestSequence=$values['PLC_Heartbeat_Req'].sequence; offset=$responsePoint.offset}
                [void](Exchange 5 $responsePoint.offset $rawBit 'PC_Heartbeat_Resp:echo')
                Event 'Info' 'WriteResponse' 'PLC -> PC FC05写入响应已核对，继续读回。' @{requestSequence=$sequence}
                Read-Group @{area='Coil';offset=$responsePoint.offset;count=1;points=@($responsePoint)}
                if ($values['PC_Heartbeat_Resp'].value -ne $bit) { throw '心跳写响应有效，但读回值不一致；检查地址、PLC逻辑或其他上位机写入。' }
                $stats.heartbeatReadbacks++
                Event 'Info' 'Readback' "PLC -> PC PC_Heartbeat_Resp读回=$bit；不代表业务动作完成。" @{requestSequence=$sequence}
            }
            $heartbeatPrevious = $bit
        }
        Advance-Motion
        if ($ActionName -and -not $automaticActionSent -and $stats.heartbeatEdges -ge 1) {
            Start-Motion $ActionName $Target
            $automaticActionSent=$true
        }
        if ($dashboard) {
            while ([Console]::KeyAvailable) {
                $key=[Console]::ReadKey($true)
                if (-not $AllowMotion) {
                    if ($key.Key -eq [ConsoleKey]::Q) { $status='Stopped' }
                    continue
                }
                if ($key.Key -eq [ConsoleKey]::Backspace -and $commandBuffer.Length -gt 0) { $commandBuffer=$commandBuffer.Substring(0,$commandBuffer.Length-1) }
                elseif ($key.Key -eq [ConsoleKey]::Enter) {
                    $command=$commandBuffer.Trim(); $commandBuffer=''
                    if ($command -eq 'quit' -and $null -eq $action) { $status='Stopped' }
                    elseif ($command -match '^move\s+(\w+)\s+(-?\d+(?:\.\d+)?)$') {
                        $axis=$Matches[1]; $position=[double]::Parse($Matches[2],[Globalization.CultureInfo]::InvariantCulture)
                        # Validation refusals keep heartbeats alive; uncertain dispatched writes stop the session.
                        $actionBefore=$action
                        try { Start-Motion $axis $position }
                        catch { if ($null -eq $actionBefore -and $null -ne $action) { throw }; Event 'Warning' 'ActionBlocked' $_.Exception.Message }
                    } else { Event 'Warning' 'Command' '输入 move 轴名 目标数值；动作执行时不能退出，须先完成或现场处置。' }
                } elseif (-not [char]::IsControl($key.KeyChar)) { $commandBuffer += $key.KeyChar }
                $nextBoardMs=0
            }
        }
        if ($status -eq 'Stopped') { break }
        if ($watch.ElapsedMilliseconds -ge $nextBoardMs) { Show-Board '采集中'; $nextBoardMs=$watch.ElapsedMilliseconds+1000 }
        $period=$c.intervalMs
        if ($null -ne $action) { $period=200; if ($action.phase -eq 'AwaitMoving') { $period=50 } }
        $delay = $period - ($watch.ElapsedMilliseconds-$cycleStart)
        if ($delay -gt 0) { Start-Sleep -Milliseconds $delay }
    }
    if ($null -ne $action) { throw '诊断时长结束时动作仍未完成，状态未知；没有自动重发或清零，须现场处置。' }
    if ($ActionName -and -not $automaticActionSent) { throw '未取得心跳变化，指定动作没有执行。' }
    if ($stats.readResponses -eq 0) { throw '本轮没有有效读取响应，不能记录为完成。' }
    if ($status -eq 'Running') { $status='Completed' }
    Event 'Info' 'Stop' "诊断结束：$status。最后样本停止更新。" $stats
} catch {
    $exitCode = 1; $failure = $_.Exception.Message; $status='Failed'
    if ($null -ne $eventLog) { Event 'Error' 'Failure' $failure @{requestSequence=$sequence} }
    Write-Host $failure -ForegroundColor Red
} finally {
    if ($null -ne $client) { $client.Close() }
    if ($null -ne $eventLog) {
        try {
            Show-Board $status
            $mean = 0.0; if ($stats.responseCount -gt 0) { $mean=$stats.totalResponseMs/$stats.responseCount }
            $summary = [ordered]@{ schemaVersion=1; sessionId=$sessionId; purpose=$c.purpose; startedUtc=$startedUtc
                endedUtc=[DateTimeOffset]::UtcNow.ToString('o'); status=$status; error=$failure; mode=$mode
                configSha256=(File-Sha256 (Join-Path $runDir 'config.snapshot.json'))
                requests=$sequence; statistics=$stats; unfinishedAction=$action; meanResponseMs=[math]::Round($mean,2)
                interpretation='仅为本轮已记录的通信/单轴观察；不证明完整工艺、配方、相机、算法或整机验收。停止后的值为最后样本。' }
            [IO.File]::WriteAllText((Join-Path $runDir 'summary.json'),($summary | ConvertTo-Json -Depth 8),$utf8)
            Write-Host "结果：$status | 日志：$runDir"
        } finally { $eventLog.Dispose(); if ($null -ne $wireLog) { $wireLog.Dispose() } }
    }
}
exit $exitCode
