param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [ValidateSet('Import','Windows','Web','Linux','Mac','Android','IOS')][string]$Task='Windows',
    [int]$TimeoutSeconds=2400
)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$version=Get-Content (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Raw
if($version -notmatch 'm_EditorVersion: 6000.3.24f1') { throw 'Unexpected project version' }
if(-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)){throw 'Unity Editor executable not found'}
& python (Join-Path $PSScriptRoot 'check_source.py')
if($LASTEXITCODE -ne 0){throw 'Source integrity failed; Unity was not started'}
$logDirectory=Join-Path $project 'Logs'
New-Item -ItemType Directory -Force $logDirectory | Out-Null
$log=Join-Path $logDirectory ($Task+'-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff')+'.log')
$method=if($Task -eq 'Import'){''}else{'FlatsPortalBuild.'+$Task}
$arguments='-batchmode -quit -projectPath "'+$project+'" -logFile "'+$log+'"'
if($method){$arguments+=' -executeMethod '+$method}
$started=[DateTime]::UtcNow
$process=Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit($TimeoutSeconds*1000)){Stop-Process -Id $process.Id;throw "Unity timed out; see $log"}
if($process.ExitCode -ne 0){throw "Unity exit $($process.ExitCode); see $log"}
if($Task -in @('Windows','Web','Linux','Mac','Android','IOS') -and -not (Select-String -LiteralPath $log -SimpleMatch 'FLATS_PORTAL_BUILD_SUCCEEDED' -Quiet)){throw "Missing build marker: $log"}
Write-Output "PASS $Task : $log"
