param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [ValidateSet('Import','Validate','Windows','Web','Linux','Mac','Android','IOS')][string]$Task='Validate',
    [int]$TimeoutSeconds=2400
)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$version=Get-Content (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Raw
if($version -notmatch 'm_EditorVersion: 6000.3.24f1') { throw 'Unexpected project version' }
if(-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)){throw 'Unity Editor executable not found'}
$logDirectory=Join-Path $project 'Logs'
New-Item -ItemType Directory -Force $logDirectory | Out-Null
$log=Join-Path $logDirectory ($Task+'-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff')+'.log')
$method=switch($Task){'Import'{''};'Validate'{'FlatsDeveloperValidation.Run'};default{'FlatsPortalBuild.'+$Task}}
$arguments='-batchmode -quit -projectPath "'+$project+'" -logFile "'+$log+'"'
if($method){$arguments+=' -executeMethod '+$method}
$process=Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit($TimeoutSeconds*1000)){Stop-Process -Id $process.Id;throw "Unity timed out; see $log"}
if($process.ExitCode -ne 0){throw "Unity exit $($process.ExitCode); see $log"}
if($Task -eq 'Validate' -and -not (Select-String -LiteralPath $log -SimpleMatch 'FLATS_DEVELOPER_VALIDATION_PASS' -Quiet)){throw "Missing validation marker: $log"}
if($Task -notin @('Import','Validate') -and -not (Select-String -LiteralPath $log -SimpleMatch 'FLATS_PORTAL_BUILD_SUCCEEDED' -Quiet)){throw "Missing build marker: $log"}
Write-Output "PASS $Task : $log"
