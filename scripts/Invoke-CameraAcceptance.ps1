param(
    [Parameter(Mandatory)][string]$HeadersPath,
    [Parameter(Mandatory)][string]$EvidencePath,
    [string]$BaseUrl = 'http://127.0.0.1:5189',
    [string[]]$Roles = @('A','B','C','D','E','F','3D'),
    [int]$Count = 3
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $EvidencePath) { throw 'Use a new evidence directory.' }
New-Item -ItemType Directory -Path $EvidencePath | Out-Null
$headers = Get-Content -LiteralPath $HeadersPath -Raw | ConvertFrom-Json -AsHashtable
$initial = Invoke-RestMethod "$BaseUrl/api/v1/cameras" -Headers $headers
$initial | ConvertTo-Json -Depth 15 | Set-Content (Join-Path $EvidencePath 'initial.json') -Encoding utf8
$checks = @()
foreach ($role in $Roles) {
    $before = $initial.cameras | Where-Object role -eq $role
    if ($before.state -ne 'Ready') { throw "Camera not ready: $role. No automatic recovery or replay." }
    $lastFrame = $null
    for ($index=1; $index -le $Count; $index++) {
        $capture = Invoke-RestMethod -Method Post "$BaseUrl/api/v1/cameras/$role/captures" -Headers $headers -TimeoutSec 65
        $capture | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $EvidencePath "$role-$index.json") -Encoding utf8
        $m = $capture.metadata
        if ($m.workerSessionId -ne $before.sessionId -or ($null -ne $lastFrame -and $m.frameId -le $lastFrame)) { throw 'Session/freshness mismatch.' }
        $lastFrame = $m.frameId
        $file = Join-Path $EvidencePath "$role-$index.content"
        Invoke-WebRequest "$BaseUrl/api/v1/camera-media/$($capture.media.mediaId)/content" -Headers $headers -OutFile $file | Out-Null
        if ((Get-Item -LiteralPath $file).Length -ne $m.payloadBytes) { throw 'Payload length mismatch.' }
        $sha = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
        if ($role -eq '3D') {
            $zip = [IO.Compression.ZipFile]::OpenRead($file)
            try {
                foreach ($payload in $m.payloads) {
                    $entry = $zip.GetEntry($payload.name)
                    if ($null -eq $entry -or $entry.Length -ne $payload.byteLength) { throw '3D payload missing or invalid.' }
                    $stream=$entry.Open()
                    try { $entrySha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
                    if ($entrySha -ne $payload.sha256) { throw '3D payload digest mismatch.' }
                }
                if ($null -eq $zip.GetEntry('metadata.json') -or $m.payloads.Count -ne 3) { throw '3D bundle incomplete.' }
            } finally { $zip.Dispose() }
        } elseif ($sha -ne $m.payloads[0].sha256) { throw '2D digest mismatch.' }
        $checks += @{role=$role;mediaId=$capture.media.mediaId;frameId=$m.frameId;session=$m.workerSessionId;sha256=$sha;bytes=$m.payloadBytes;verified=$true}
    }
}
$final = Invoke-RestMethod "$BaseUrl/api/v1/cameras" -Headers $headers
foreach ($role in $Roles) {
    $a=$initial.cameras | Where-Object role -eq $role; $b=$final.cameras | Where-Object role -eq $role
    if ($a.processId -ne $b.processId -or $a.sessionId -ne $b.sessionId -or $a.openCount -ne $b.openCount -or $b.state -ne 'Ready') { throw "Connection reuse failed: $role" }
}
$final | ConvertTo-Json -Depth 15 | Set-Content (Join-Path $EvidencePath 'final.json') -Encoding utf8
$checks | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $EvidencePath 'checks.json') -Encoding utf8
Write-Output "Verified $($checks.Count) new frames; restart-read and normal restoration require separate checks."
