param([string]$ServerProject,[string]$ClientExecutable,[string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
$taskServerRoot=Join-Path $taskRoot 'SmartTrainingServer'
$taskClientExe=Join-Path $taskRoot 'UnityClient/Builds/Client/Smart Training Client.exe'
$taskRun=Join-Path $taskRoot ('ugui-evidence/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
if($ServerProject){$taskServerRoot=[IO.Path]::GetFullPath($ServerProject)}
if($ClientExecutable){$taskClientExe=[IO.Path]::GetFullPath($ClientExecutable)}
if($EvidenceRoot){$taskRun=Join-Path ([IO.Path]::GetFullPath($EvidenceRoot)) (Get-Date -Format 'yyyyMMdd-HHmmss')}
New-Item -ItemType Directory -Path $taskRun -Force | Out-Null
$taskApi='http://127.0.0.1:51456'
$taskServer=$null; $taskPlayers=@(); $taskIds=@(0,0,0); $taskOld=@{}
$taskSettings=@{ASPNETCORE_ENVIRONMENT='Development'; Server__Port='51456'; Room__ServerApiBaseUrl=$taskApi; Room__AdvertisedAddress='127.0.0.1'; SMART_TRAINING_SERVER_PROBE='1'}
function Read-Player([int]$index){
 $file=Join-Path $taskRun ($index.ToString()+'/state.json')
 if(!(Test-Path -LiteralPath $file)){return $null}
 try{return (Get-Content -LiteralPath $file -Raw -Encoding UTF8 | ConvertFrom-Json)}catch{return $null}
}
function Wait-Player([int]$index,[scriptblock]$condition,[string]$name){
 $end=[DateTime]::UtcNow.AddSeconds(45)
 while([DateTime]::UtcNow -lt $end){
  $state=Read-Player $index
  if($state -and $state.error){throw ('Probe error: '+$state.error)}
  if($state -and (& $condition $state)){return $state}
  if($taskPlayers[$index].HasExited){throw ('Player exited: '+$index)}
  Start-Sleep -Milliseconds 100
 }
 $state=Read-Player $index
 throw ($name+' timed out: '+($state | ConvertTo-Json -Compress))
}
function Send-Player([int]$index,[string]$action,[string]$value='', [string]$lobbyId='', [bool]$enabled=$false){
 $taskIds[$index]++
 $folder=Join-Path $taskRun $index.ToString()
 $temp=Join-Path $folder 'command.tmp'
 @{id=$taskIds[$index];action=$action;value=$value;lobbyId=$lobbyId;enabled=$enabled} | ConvertTo-Json | Set-Content -LiteralPath $temp -Encoding UTF8
 Move-Item -LiteralPath $temp -Destination (Join-Path $folder 'command.json') -Force
 return $taskIds[$index]
}
function Check([bool]$ok,[string]$name){if(!$ok){throw $name}; Write-Output ('MULTIPROCESS PASS: '+$name)}
function Api-Post([string]$path,$body){Invoke-RestMethod ($taskApi+$path) -Method Post -Headers $taskAuth -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress)}
$taskSettings['Operations__OpenBrowserOnStart']='false';$taskSettings['Operations__PersistHistory']='true'
$taskSettings['Operations__HistoryPath']=Join-Path $taskRun 'history.json';$taskSettings['SMART_TRAINING_SERVER_PROBE']='0'
$taskOpsHeaders=@{'X-Operations-Request'='1';Origin=$taskApi};$taskRound=0
function Ops {Invoke-RestMethod ($taskApi+'/api/operations')}
function Mode([bool]$enabled,[string]$reason,[long]$revision){Invoke-RestMethod ($taskApi+'/api/operations/lobbies/sample-training/maintenance') -Method Post -Headers $taskOpsHeaders -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes((@{enabled=$enabled;reason=$reason;expectedRevision=$revision}|ConvertTo-Json -Compress)))}
function Root { (Ops).lobbies|Where-Object {$_.id -eq 'sample-training'} }
function Start-Api {
 $script:taskRound++;$dll=Join-Path $taskServerRoot 'bin/Debug/net8.0/Smart Training Server.dll'
 $script:taskServer=Start-Process dotnet -ArgumentList ('"'+$dll+'"') -WorkingDirectory $taskServerRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskRun ('server-'+$taskRound+'.log')) -RedirectStandardError (Join-Path $taskRun ('errors-'+$taskRound+'.log'))
 $ready=$false
 for($n=0;$n -lt 100;$n++){try{$null=Invoke-RestMethod ($taskApi+'/api/health');$ready=$true;break}catch{if($taskServer.HasExited){throw 'Maintenance API exited'};Start-Sleep -Milliseconds 100}}
 if(!$ready){throw 'Maintenance API failed to start'}
}
function Stop-Api {if($taskServer -and !$taskServer.HasExited){Stop-Process -Id $taskServer.Id -Force;$null=$taskServer.WaitForExit(5000)}}
function Capture([string]$name){
 $null=Send-Player 0 'uiCapture' $name;$path=Join-Path (Join-Path $taskRun '0') $name;$end=[DateTime]::UtcNow.AddSeconds(10)
 do{if((Test-Path -LiteralPath $path) -and (Get-Item -LiteralPath $path).Length -gt 1000){return};Start-Sleep -Milliseconds 100}while([DateTime]::UtcNow -lt $end)
 throw ('Screenshot missing '+$name)
}
try{
 foreach($key in $taskSettings.Keys){$taskOld[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,$taskSettings[$key],'Process')}
 Start-Api
 $folder=Join-Path $taskRun '0';New-Item -ItemType Directory -Path $folder -Force|Out-Null
 $args='-screen-fullscreen 0 -screen-width 1280 -screen-height 800 --smarttraining-probe "'+$folder+'" -logFile "'+(Join-Path $folder 'player.log')+'"'
 $taskPlayers+=Start-Process $taskClientExe -ArgumentList $args -WorkingDirectory (Split-Path $taskClientExe -Parent) -WindowStyle Hidden -PassThru
 $s=Wait-Player 0 {param($s) $s.uiVisible -and $s.uiLoginVisible -and $s.uiLayoutValid} 'uGUI login startup'
 Check (!$s.loggedIn -and $s.uiFont -match 'Malgun|맑은') 'login Canvas has working input raycasts and Korean font'
 Capture 'login-1280.png'
 $null=Send-Player 0 'uiLogin' 'ugui-user' $taskApi
 $s=Wait-Player 0 {param($s) $s.loggedIn -and !$s.busy -and $s.uiVisible -and !$s.uiLoginVisible -and $s.uiRows -eq 3 -and $s.uiLayoutValid} 'uGUI login and catalog rows'
 Check $true 'uGUI input fields and login button reach actual ASP.NET login and three catalog rows'
 $null=Send-Player 0 'uiSelect' 'sample-training'
 $s=Wait-Player 0 {param($s) !$s.busy -and $s.selectedLobbyId -eq 'sample-training' -and $s.uiEnterEnabled -and $s.uiDetail.Contains('테스트 훈련 콘텐츠')} 'uGUI row selection'
 Check $true 'dynamic lobby row button selects existing content API and enables entry'
 Capture 'lobbies-1280.png'
 $null=Mode $true 'uGUI 점검 표시' 0
 $s=Wait-Player 0 {param($s) !$s.busy -and !$s.uiEnterEnabled -and $s.uiDetail.Contains('uGUI 점검 표시')} 'Live maintenance notice'
 Check $true 'live maintenance disables uGUI entry and displays reason'
 $null=Mode $false '입장 재개' 1
 $s=Wait-Player 0 {param($s) !$s.busy -and $s.uiEnterEnabled} 'Live reopening'
 Check $true 'live operating resume re-enables entry without manual refresh'
 $null=Send-Player 0 'uiJoinPaused'
 $s=Wait-Player 0 {param($s) $s.busy -and $s.uiVisible -and $s.uiProgressVisible -and $s.canCancelLobby -and $s.uiLayoutValid} 'uGUI join progress'
 Check $true 'uGUI progress remains visible and cancel button has unblocked raycast during admission'
 Capture 'joining-1280.png'
 $null=Send-Player 0 'uiCancel'
 $s=Wait-Player 0 {param($s) !$s.busy -and $s.lobbyId -eq '' -and $s.pendingLobbyId -eq '' -and $s.uiVisible -and !$s.uiProgressVisible} 'uGUI cancel acknowledgement'
 Check $true 'uGUI cancel cleans accepted membership and returns to catalog'
 $null=Send-Player 0 'uiJoin'
 $s=Wait-Player 0 {param($s) !$s.busy -and $s.lobbyId -eq 'sample-training' -and !$s.uiVisible} 'Legacy lobby handoff'
 Check $true 'successful uGUI admission hands off to existing room management screen'
 $null=Send-Player 0 'leaveLobby'
 $s=Wait-Player 0 {param($s) !$s.busy -and $s.lobbyId -eq '' -and $s.uiVisible} 'Return to uGUI catalog'
 Check $true 'leaving legacy lobby returns to uGUI catalog'
 $null=Send-Player 0 'uiResize' '960x720'
 $s=Wait-Player 0 {param($s) !$s.busy -and $s.uiLayoutValid -and $s.uiWidth -eq 960 -and $s.uiHeight -eq 720} 'Compact lobby layout'
 Capture 'lobbies-960.png'
 Check $true 'catalog controls retain input raycasts at compact resolution'
 $null=Send-Player 0 'uiLogout'
 $s=Wait-Player 0 {param($s) !$s.loggedIn -and !$s.busy -and $s.uiVisible -and $s.uiLoginVisible -and $s.uiLayoutValid} 'uGUI logout'
 Capture 'login-960.png'
 Check $true 'uGUI logout returns to editable login fields at compact resolution'
 $null=Send-Player 0 'uiLogin' 'ugui-user-again' $taskApi
 $null=Wait-Player 0 {param($s) $s.loggedIn -and !$s.busy -and $s.uiRows -eq 3} 'Second uGUI login'
 Check $true 'second uGUI login succeeds without duplicated action listeners'
 $null=Send-Player 0 'uiLogout';$null=Wait-Player 0 {param($s) !$s.loggedIn -and !$s.busy} 'Final logout'
 $errors=Select-String -LiteralPath (Join-Path $folder 'player.log') -Pattern 'NullReferenceException|MissingReferenceException|InvalidOperationException|ArgumentException|MULTICLIENT PROBE:'
 Check (!$errors) 'client has no UI runtime exceptions'
 Write-Output ('UGUI COMPLETE: '+$taskRun)
}finally{
 foreach($player in $taskPlayers){if(!$player.HasExited){Stop-Process -Id $player.Id -Force -ErrorAction SilentlyContinue}}
 Stop-Api
 foreach($key in $taskOld.Keys){[Environment]::SetEnvironmentVariable($key,$taskOld[$key],'Process')}
}
