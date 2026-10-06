param([int]$ModbusPort = 1603, [int]$ApiPort = 5181)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$env:Modbus__Port = [string]$ModbusPort
$env:Dashboard__OpenBrowserOnStart = 'false'
$dll = Join-Path $repo 'VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll'
$process = Start-Process dotnet -ArgumentList ('"' + $dll + '" --urls http://127.0.0.1:' + $ApiPort) `
    -WorkingDirectory (Join-Path $repo 'VirtualPlc') -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Seconds 2
    1..7 | ForEach-Object {
        $state = Invoke-RestMethod "http://127.0.0.1:$ApiPort/api/simulator/state"
        $bit = $state.coils | Where-Object name -eq 'PLC_Heartbeat_Req'
        [pscustomobject]@{ timestampUtc = [datetime]::UtcNow.ToString('o'); heartbeat = $bit.value;
            communicationTimedOut = $state.communicationTimedOut }
        Start-Sleep -Milliseconds 700
    }
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
}
