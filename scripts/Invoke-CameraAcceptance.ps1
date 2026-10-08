param(
    [Parameter(Mandatory)][string]$HeadersPath,
    [Parameter(Mandatory)][string]$EvidencePath,
    [Parameter(Mandatory)][string]$SitePath,
    [string]$BaseUrl = 'http://127.0.0.1:5189',
    [string[]]$Roles = @('A','B','C','D','E','F','3D'),
    [int]$Count = 3
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $EvidencePath) { throw 'Use a new evidence directory.' }
New-Item -ItemType Directory -Path $EvidencePath | Out-Null
$headers = Get-Content -LiteralPath $HeadersPath -Raw | ConvertFrom-Json -AsHashtable
$bindings = (Get-Content -LiteralPath $SitePath -Raw | ConvertFrom-Json).Cameras
$initial = Invoke-RestMethod "$BaseUrl/api/v1/cameras" -Headers $headers
$initial | ConvertTo-Json -Depth 15 | Set-Content (Join-Path $EvidencePath 'initial.json') -Encoding utf8
$checks = @()
foreach ($role in $Roles) {
    $before = $initial.cameras | Where-Object role -eq $role
    $binding = @($bindings | Where-Object Role -eq $role)
    if ($binding.Count -ne 1 -or $before.serial -ne $binding[0].Serial -or
        $before.expectedNicMac.Replace('-','').Replace(':','') -ne $binding[0].ExpectedNicMac.Replace('-','').Replace(':','')) { throw 'Configured camera identity mismatch.' }
    if ($before.state -ne 'Ready') { throw "Camera not ready: $role. No automatic recovery or replay." }
    $lastFrame = $null
    for ($index=1; $index -le $Count; $index++) {
        $capture = Invoke-RestMethod -Method Post "$BaseUrl/api/v1/cameras/$role/captures" -Headers $headers -TimeoutSec 65
        $capture | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $EvidencePath "$role-$index.json") -Encoding utf8
        $m = $capture.metadata
        if ($m.role -ne $role -or $m.serial -ne $binding[0].Serial -or
            $m.nicMac.Replace('-','').Replace(':','') -ne $binding[0].ExpectedNicMac.Replace('-','').Replace(':','') -or
            $m.width -le 0 -or $m.height -le 0 -or $capture.media.byteLength -ne $m.payloadBytes) { throw 'Returned device identity/dimensions mismatch.' }
        if ($m.workerSessionId -ne $before.sessionId -or ($null -ne $lastFrame -and $m.frameId -le $lastFrame)) { throw 'Session/freshness mismatch.' }
        $lastFrame = $m.frameId
        $file = Join-Path $EvidencePath "$role-$index.content"
        Invoke-WebRequest "$BaseUrl/api/v1/camera-media/$($capture.media.mediaId)/content" -Headers $headers -OutFile $file | Out-Null
        if ((Get-Item -LiteralPath $file).Length -ne $m.payloadBytes) { throw 'Payload length mismatch.' }
        $sha = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
        if ($role -eq '3D') {
            if ($capture.media.format -ne 'CameraProFrameZipV1') { throw '3D format mismatch.' }
            $p=$m.actualParameters
            $pixels=[long]$p.irWidth * [long]$p.irHeight
            if ([long]$p.irWidth -ne $m.width -or [long]$p.irHeight -ne $m.height -or [int]$p.pixelBytes -notin @(1,2)) { throw '3D dimensions/pixel type invalid.' }
            $depthPixels=switch ([int]$p.depthType) {1 { [long]$p.textureWidth * [long]$p.textureHeight }; 2 {$pixels}; default {throw 'Unsupported depth layout.'}}
            $groups=switch ([int]$p.reconstructionType) {0 {2};2 {1};default {throw 'Unsupported IR camera layout.'}}
            if ($depthPixels -le 0 -or [int]$p.irImagesPerCamera -ne 2 -or [int]$p.irCameraGroups -ne $groups -or
                [long]$p.irImageBytes -ne $pixels*[int]$p.pixelBytes -or $p.byteOrder -ne 'little-endian' -or
                $m.pixelFormat -ne "CameraPro XYZ-f32 Depth-f32 IR-u$([int]$p.pixelBytes*8)") {throw '3D element layout mismatch.'}
            # Expected channel names and element types come from the SDK contract, never from returned payloads.
            $expected=@(
                @{name='points.xyz.f32';elements=$pixels*3;elementBytes=4},
                @{name='depth.f32';elements=$depthPixels;elementBytes=4},
                @{name='ir.bytes';elements=$pixels*2*$groups;elementBytes=[int]$p.pixelBytes}
            )
            $zip = [IO.Compression.ZipFile]::OpenRead($file)
            try {
                if ($zip.Entries.Count -ne 4 -or @($zip.Entries.FullName | Select-Object -Unique).Count -ne 4 -or $m.payloads.Count -ne 3) {throw '3D required channels invalid.'}
                foreach ($expect in $expected) {
                    $payload=@($m.payloads | Where-Object name -eq $expect.name)
                    $entry = $zip.GetEntry($expect.name)
                    if ($payload.Count -ne 1 -or $null -eq $entry -or $entry.Length -ne $expect.elements*$expect.elementBytes -or
                        $payload[0].elementCount -ne $expect.elements -or $payload[0].elementBytes -ne $expect.elementBytes -or
                        $payload[0].byteLength -ne $entry.Length) { throw '3D payload missing or invalid.' }
                    $stream=$entry.Open()
                    try { $entrySha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
                    if ($entrySha -ne $payload[0].sha256) { throw '3D payload digest mismatch.' }
                }
                $manifestEntry=$zip.GetEntry('metadata.json')
                if ($null -eq $manifestEntry -or $manifestEntry.Length -gt 1048576) { throw '3D bundle manifest missing/oversized.' }
                $reader=[IO.StreamReader]::new($manifestEntry.Open())
                try {$inner=$reader.ReadToEnd() | ConvertFrom-Json} finally {$reader.Dispose()}
                if ($inner.role -ne $role -or $inner.serial -ne $m.serial -or $inner.workerSessionId -ne $m.workerSessionId -or
                    $inner.frameIndex -ne $m.frameId -or $inner.triggerSequence -ne $m.triggerSequence -or
                    $inner.frameTimestamp -ne $m.deviceTimestamp -or
                    $inner.rawPayloadBytes -ne ($m.payloads | Measure-Object byteLength -Sum).Sum -or
                    ($inner.payloads | ConvertTo-Json -Depth 5 -Compress) -ne ($m.payloads | ConvertTo-Json -Depth 5 -Compress)) {throw '3D manifest/frame identity mismatch.'}
            } finally { $zip.Dispose() }
        } else {
            $p=$m.actualParameters
            $pixel=$m.pixelFormat.ToUpperInvariant().Replace('GX_PIXEL_FORMAT_','').Replace('_','')
            if ($capture.media.format -ne 'GalaxyRaw' -or [long]$p.Width -ne $m.width -or [long]$p.Height -ne $m.height -or
                [long]$p.PayloadSize -ne $m.payloadBytes -or $pixel -ne $p.PixelFormat.ToUpperInvariant().Replace('_','') -or
                $m.payloads.Count -ne 1 -or $m.payloads[0].name -ne 'frame.raw' -or $m.payloads[0].elementBytes -ne 1 -or
                $m.payloads[0].elementCount -ne $m.payloadBytes -or $m.payloads[0].byteLength -ne $m.payloadBytes -or $sha -ne $m.payloads[0].sha256) {throw '2D raw layout/digest mismatch.'}
            $containerBytes=switch($pixel){'MONO8'{1};'MONO10'{2};'MONO12'{2};'MONO16'{2};'RGB8'{3};'BGR8'{3};'RGB8PACKED'{3};'BGR8PACKED'{3};default{0}}
            if($containerBytes -eq 0){throw "Pixel layout requires an independent acceptance rule: $pixel; SDK size alone does not prove its geometry."}
            if($containerBytes -gt 0 -and $m.payloadBytes -ne [long]$m.width*$m.height*$containerBytes){throw '2D dimensions incompatible with pixel container.'}
            # Unknown/packed formats are checked against actual SDK payload size, not guessed as one byte per pixel.
        }
        $persisted=Invoke-RestMethod "$BaseUrl/api/v1/camera-media/$($capture.media.mediaId)" -Headers $headers
        if ($persisted.media.captureId -ne $capture.media.captureId -or $persisted.metadata.workerSessionId -ne $m.workerSessionId -or
            $persisted.metadata.frameId -ne $m.frameId) {throw 'Committed media/metadata lookup mismatch.'}
        $checks += @{role=$role;serial=$m.serial;mediaId=$capture.media.mediaId;captureId=$capture.media.captureId;runId=$capture.media.runId;relativeKey=$capture.media.relativeKey;frameId=$m.frameId;session=$m.workerSessionId;sha256=$sha;bytes=$m.payloadBytes;verified=$true}
    }
}
$final = Invoke-RestMethod "$BaseUrl/api/v1/cameras" -Headers $headers
$index=Invoke-RestMethod "$BaseUrl/api/v1/camera-media" -Headers $headers
foreach($check in $checks){if($check.mediaId -notin $index.mediaId){throw 'Media missing from durable index.'}}
foreach ($role in $Roles) {
    $a=$initial.cameras | Where-Object role -eq $role; $b=$final.cameras | Where-Object role -eq $role
    if ($a.processId -ne $b.processId -or $a.sessionId -ne $b.sessionId -or $a.openCount -ne $b.openCount -or $b.state -ne 'Ready') { throw "Connection reuse failed: $role" }
}
$final | ConvertTo-Json -Depth 15 | Set-Content (Join-Path $EvidencePath 'final.json') -Encoding utf8
$checks | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $EvidencePath 'checks.json') -Encoding utf8
Write-Output "Verified $($checks.Count) new frames; restart-read and normal restoration require separate checks."
