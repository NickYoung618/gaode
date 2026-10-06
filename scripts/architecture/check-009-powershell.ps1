$ErrorActionPreference='Stop'
[Console]::InputEncoding=[Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$inputs009=[Console]::In.ReadToEnd() | ConvertFrom-Json -Depth 100
$results009=@()
foreach($input009 in $inputs009){
    $tokens009=$null
    $errors009=$null
    if(Test-Path -LiteralPath $input009.path -PathType Leaf){
        $ast009=[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path -LiteralPath $input009.path).Path,[ref]$tokens009,[ref]$errors009)
    }else{
        $ast009=[System.Management.Automation.Language.Parser]::ParseInput($input009.source,[ref]$tokens009,[ref]$errors009)
    }
    $violations009=[System.Collections.Generic.List[object]]::new()
    $aliases009=[System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $fields009=@('alarmBits','alarmSeverity','protocolStatus','inspectionStatus','zResetStatus','flipStatus','palletLockStatus','plcSystemFault','reliableFeedback','sortingAckCleared','documentNumber','pduOffset','registerAddress','rawWords','rawBytes','positionEvidence')
    $communication009=$input009.role -eq 'communication-probe'
    $handshakeFacts009=(Get-Content -LiteralPath (Join-Path $PSScriptRoot '009-script-boundary-cases.json') -Raw | ConvertFrom-Json).internalHandshakeIdentities
    if(!$communication009){
        foreach($literal009 in $ast009.FindAll({param($n) $n -is [System.Management.Automation.Language.StringConstantExpressionAst]},$true)){
            if($literal009.Value -in $handshakeFacts009){
                $violations009.Add(@{ruleId='A08';path=$input009.path;line=$literal009.Extent.StartLineNumber;column=$literal009.Extent.StartColumnNumber;sourcePath=$input009.path;message='Known internal handshake identity in protected business assertion.'})
            }
        }
    }
    $members009=$ast009.FindAll({param($n) $n -is [System.Management.Automation.Language.MemberExpressionAst]},$true)
    foreach($member009 in $members009){
        if($member009.Member -is [System.Management.Automation.Language.StringConstantExpressionAst] -and $member009.Member.Value -in $fields009 -and !$communication009){
            $violations009.Add(@{ruleId='A08';path=$input009.path;line=$member009.Extent.StartLineNumber;column=$member009.Extent.StartColumnNumber;sourcePath=$input009.path;message='Raw field in protected PowerShell orchestration; migrate wire assertion to classified probe.'})
        }
    }
    function Test-Protected009($node009){
        $matches009=$node009.FindAll({param($n)
            ($n -is [System.Management.Automation.Language.MemberExpressionAst] -and
             $n.Member -is [System.Management.Automation.Language.StringConstantExpressionAst] -and
             ($n.Member.Value -in $fields009 -or $n.Member.Value -in @('plc','startupDiagnostic','semanticObservation'))) -or
            ($n -is [System.Management.Automation.Language.VariableExpressionAst] -and $aliases009.Contains($n.VariablePath.UserPath))
        },$true)
        return $matches009.Count -gt 0
    }
    do{
        $changed009=$false
        foreach($assignment009 in $ast009.FindAll({param($n) $n -is [System.Management.Automation.Language.AssignmentStatementAst]},$true)){
            if((Test-Protected009 $assignment009.Right) -and $assignment009.Left -is [System.Management.Automation.Language.VariableExpressionAst]){
                if($aliases009.Add($assignment009.Left.VariablePath.UserPath)){$changed009=$true}
            }
        }
    }while($changed009)
    foreach($binary009 in $ast009.FindAll({param($n) $n -is [System.Management.Automation.Language.BinaryExpressionAst]},$true)){
        $tainted009=$binary009.FindAll({param($n) $n -is [System.Management.Automation.Language.VariableExpressionAst] -and $aliases009.Contains($n.VariablePath.UserPath)},$true)
        if($tainted009.Count -gt 0 -and !$communication009){$violations009.Add(@{ruleId='A08';path=$input009.path;line=$binary009.Extent.StartLineNumber;column=$binary009.Extent.StartColumnNumber;sourcePath=$input009.path;message='Raw alias comparison in orchestration.'})}
    }
    $dependencies009=@()
    foreach($command009 in $ast009.FindAll({param($n) $n -is [System.Management.Automation.Language.CommandAst]},$true)){
        if(!$communication009 -and (Test-Protected009 $command009)){
            $name009=$command009.GetCommandName()
            if($name009 -notin @('ConvertTo-Json','Set-Content','Out-File','Write-Output','Where-Object')){
                $violations009.Add(@{ruleId='A09';path=$input009.path;line=$command009.Extent.StartLineNumber;column=$command009.Extent.StartColumnNumber;sourcePath=$input009.path;message='Protected device object escapes to unsupported PowerShell command/helper.'})
            }
        }
        foreach($element009 in $command009.CommandElements){
            if($element009 -is [System.Management.Automation.Language.StringConstantExpressionAst] -and $element009.Value -match '\.(ps1|psm1|py|cjs|mjs|js)$'){$dependencies009+=$element009.Value}
        }
    }
    foreach($member009 in $members009){
        if(!$communication009 -and (Test-Protected009 $member009.Expression) -and $member009.Member -isnot [System.Management.Automation.Language.StringConstantExpressionAst]){
            $violations009.Add(@{ruleId='A09';path=$input009.path;line=$member009.Extent.StartLineNumber;column=$member009.Extent.StartColumnNumber;sourcePath=$input009.path;message='Dynamic property on protected object is outside finite PowerShell orchestration.'})
        }
    }
    # Structural invocation facts are also consumed by 010. Parsing stays shared;
    # these facts do not change 009's protocol rules or classify a script as safe.
    $workflowActions009=@($ast009.FindAll({param($n)
        $n -is [System.Management.Automation.Language.InvokeMemberExpressionAst] -or
        $n -is [System.Management.Automation.Language.CommandAst]
    },$true) | ForEach-Object {
        $name009=if($_ -is [System.Management.Automation.Language.CommandAst]){$_.GetCommandName()}else{$_.Member.Value}
        @{name=$name009;line=$_.Extent.StartLineNumber;column=$_.Extent.StartColumnNumber}
    })
    $results009+=@{parserVersion=$PSVersionTable.PSVersion.ToString();parsed=$errors009.Count -eq 0;violations=@($violations009.ToArray());errors=@($errors009 | ForEach-Object {@{code='PARSE';path=$input009.path;message=$_.Message;line=$_.Extent.StartLineNumber}});dependencies=$dependencies009;workflowActions=$workflowActions009}
}
ConvertTo-Json -InputObject @($results009) -Depth 100 -Compress
