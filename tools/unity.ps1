param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [ValidateSet('Import','Validate','PlayMode','Sights','Shaders','Audio','Navigation','NavigationPlay','Windows','Web','Linux','Mac','Android','IOS')][string]$Task='Validate',
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
$method=switch($Task){'Import'{''};'Validate'{'FlatsDeveloperValidation.Run'};'PlayMode'{'FlatsPlayModeValidation.Run'};'Sights'{'FlatsSightValidation.Run'};'Shaders'{'FlatsShaderVerification.Run'};'Audio'{'FlatsAudioVerification.Run'};'Navigation'{'FlatsNavigationVerification.Run'};'NavigationPlay'{'FlatsNavigationPlayValidation.Run'};default{'FlatsPortalBuild.'+$Task}}
$arguments='-batchmode -quit -projectPath "'+$project+'" -logFile "'+$log+'"'
$previousTestRoot=$env:FLATS_MOD_TEST_ROOT
if($Task -in @('PlayMode','NavigationPlay')){
    $arguments=$arguments.Replace('-quit ','')
    $env:FLATS_MOD_TEST_ROOT=Join-Path $logDirectory ('play-profile-'+[Guid]::NewGuid().ToString('N'))
}
if($method){$arguments+=' -executeMethod '+$method}
$started=[DateTime]::UtcNow
try {$process=Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru}
finally {$env:FLATS_MOD_TEST_ROOT=$previousTestRoot}
if(-not $process.WaitForExit($TimeoutSeconds*1000)){Stop-Process -Id $process.Id;throw "Unity timed out; see $log"}
if($process.ExitCode -ne 0){throw "Unity exit $($process.ExitCode); see $log"}
if($Task -eq 'Validate' -and -not (Select-String -LiteralPath $log -SimpleMatch 'FLATS_DEVELOPER_VALIDATION_PASS' -Quiet)){throw "Missing validation marker: $log"}
if($Task -eq 'PlayMode'){
    $report=Get-Item (Join-Path $logDirectory 'playmode-validation.txt')
    if($report.LastWriteTimeUtc -lt $started -or (Get-Content $report.FullName -Raw) -notmatch '^PASS '){throw 'Missing, stale or failed Play Mode validation'}
}
if($Task -in @('Windows','Web','Linux','Mac','Android','IOS') -and -not (Select-String -LiteralPath $log -SimpleMatch 'FLATS_PORTAL_BUILD_SUCCEEDED' -Quiet)){throw "Missing build marker: $log"}
Write-Output "PASS $Task : $log"
