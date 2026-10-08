[CmdletBinding()]
param([string]$InstallationRoot=(Split-Path $PSScriptRoot),[switch]$CheckOnly)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($InstallationRoot)
$profile=Get-Content -LiteralPath (Join-Path $root 'config/runtime-profile.json') -Raw|ConvertFrom-Json
$base=[string]$profile.apiBaseUrl
$uri=[Uri]$base
if(-not $uri.IsLoopback -or $uri.Scheme -ne 'http'){throw '仅允许调用本机正式后台。'}
if($CheckOnly){Write-Output '复位入口配置存在；检查模式未连接后台或设备、未发复位。';return}
$log=Join-Path $root 'data/reset-history.jsonl'
$operationId=[Guid]::NewGuid().ToString()
function Record-Reset($state,$facts){
    @{utc=[DateTimeOffset]::UtcNow;operationId=$operationId;state=$state;facts=$facts}|ConvertTo-Json -Compress -Depth 5|Add-Content -LiteralPath $log -Encoding utf8
}
try{
    $private=Get-Content -LiteralPath (Join-Path $root 'data/private-identity/host-identities.json') -Raw|ConvertFrom-Json
    $operator=@($private.Gaode.CommissioningIdentities|Where-Object Role -eq 'Operator')
    if($operator.Count -ne 1){throw '本机操作员身份配置不唯一。'}
    $headers=@{Authorization='Bearer '+$operator[0].Credential}
    $status=Invoke-RestMethod -Uri ($base+'/api/v1/station01/status') -Headers $headers -NoProxy -TimeoutSec 5
    if($status.mode -ne 'RealDeviceCommissioning' -or $status.host -ne 'Ready'){throw '后台不是本轮联调模式或尚未就绪。'}
    if($status.stage -notin @('Idle','Blocked','RecoveryRequired','Restricted','Failed','Completed','Cancelled','Stopped')){
        throw ('当前流程状态为 '+$status.stage+'，不允许在执行中调用本入口。')
    }
    Record-Reset 'Requested' @{stage=$status.stage;runId=$status.currentRun.runId;automaticRetry=$false}
    Write-Output '正在调用正式PLC复位：先PC就绪、取消软停并读回确认，再请求PLC复位。请勿重复点击；不会自动启动或继续旧动作。'
    $result=Invoke-RestMethod -Method Post -Uri ($base+'/api/v1/station01/reset') -Headers $headers -ContentType 'application/json' -Body '{}' -NoProxy -TimeoutSec 35
    if($result.reset -ne $true){throw '后台没有确认复位完成。'}
    Record-Reset 'PlcResetCompleted' @{manualStartRequired=$true}
    Write-Output '后台已确认PLC复位完成；未发送运行启动指令。'
    if($result.recoveryClosed -eq $true){
        Record-Reset 'OldRunRecoveryClosed' @{runId=$result.runId;recoveryWriteId=$result.recoveryWriteId;manualStartRequired=$true}
        Write-Output ('旧任务 '+$result.runId+' 已经恢复核验并结束，历史记录保留。请刷新页面并核对配方，手动开始完整新一轮。')
    }
    $admission=Invoke-RestMethod -Uri ($base+'/api/v1/station01/start-admission') -Headers $headers -NoProxy -TimeoutSec 5
    Record-Reset 'AdmissionObserved' @{state=$admission.state;ownerRunId=$admission.ownerRunId;reasonCodes=$admission.reasonCodes}
    if($admission.state -eq 'Held'){
        Write-Warning ('PLC复位已成功，但旧任务 '+$admission.ownerRunId+' 仍待恢复核验，尚未放行新一轮。请在页面查看恢复操作；若没有可用入口，须维护人员补齐受控恢复流程。重开程序或反复启动无效，不要删除运行库。')
    }else{
        Write-Output '请重新核对页面安全状态和配方，由人员决定是否开始新一轮。'
    }
}catch{
    $code=$null
    try { $body=$_.ErrorDetails.Message|ConvertFrom-Json; $code=[string]$body.message } catch { }
    $detail=switch -Wildcard ($code) {
        'PreviousResetRequestNotReleased' {'上次复位请求MB2009仍为1，结果未核定。本次没有再次发送复位；需要核对上次复位记录和PLC状态，不能直接清0再重发。'}
        'ResetPreconditionsNotConfirmed*' {'未读回确认PC就绪MB2006=1及软停MB2008=0，本次未发送PLC复位请求。'}
        'ResetCompletionSafetyUnconfirmed' {'PLC就绪变化已出现，但安全状态未通过，复位未获完整确认。'}
        'RecoveryExecutionStillActive' {'旧流程尚未退出，不允许执行复位恢复。请等待流程停止后查询状态。'}
        'RecoverySoftwareResourcesNotReleased' {'旧采集、算法或媒体资源尚未释放，不能复位恢复放行。请查看后台诊断。'}
        'RecoveryInitialStateIncomplete*' {'本次复位后的安全/请求/反馈核验未通过，旧任务保持阻断。请查看后台具体缺项。'}
        'RecoveryResetDeadlineExceeded' {'等待本次PLC复位或恢复核验超时，旧任务未放行；结果未确认，不会自动重试。'}
        'StartupSafeZeroUnconfirmed*' {'PLC就绪变化已出现，但实际XYZ尚未通过安全零位检查，不能放行。'}
        default { if($code){$code}else{'调用失败或超时，复位结果尚未确认；不要反复点击。'} }
    }
    Record-Reset 'FailedOrUnknown' @{errorType=$_.Exception.GetType().Name;code=$code;automaticRetry=$false}
    Write-Error ('复位未确认成功或后续核对失败：'+$detail+' 未自动重试；请查看后台日志及data/reset-history.jsonl。')
    exit 1
}
