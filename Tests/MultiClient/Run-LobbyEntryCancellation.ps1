param([string]$ServerProject,[string]$ClientExecutable,[string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
$taskServerRoot=Join-Path $taskRoot 'SmartTrainingServer'
$taskClientExe=Join-Path $taskRoot 'UnityClient/Builds/Client/Smart Training Client.exe'
$taskRun=Join-Path $taskRoot ('independent-clients/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
if($ServerProject){$taskServerRoot=[IO.Path]::GetFullPath($ServerProject)}
if($ClientExecutable){$taskClientExe=[IO.Path]::GetFullPath($ClientExecutable)}
if($EvidenceRoot){$taskRun=Join-Path ([IO.Path]::GetFullPath($EvidenceRoot)) (Get-Date -Format 'yyyyMMdd-HHmmss')}
New-Item -ItemType Directory -Path $taskRun -Force | Out-Null
$taskApi='http://127.0.0.1:51420'
$taskServer=$null; $taskPlayers=@(); $taskIds=@(0,0,0); $taskOld=@{}
$taskSettings=@{ASPNETCORE_ENVIRONMENT='Development'; Server__Port='51420'; Room__ServerApiBaseUrl=$taskApi; Room__AdvertisedAddress='127.0.0.1'; SMART_TRAINING_SERVER_PROBE='1'}
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
$taskSettings['Contents__Directory']=$taskRun
@'
[{"rootContent":"sample-training","id":1,"title":"Sample","children":[]},
 {"rootContent":"failed-test","id":10,"title":"Failure fixture","lobbyServerExecutablePath":"missing-lobby-fixture.exe","children":[]}]
'@ | Set-Content -LiteralPath (Join-Path $taskRun 'catalog.json') -Encoding UTF8
function Lobby-Summary{Invoke-RestMethod ($taskApi+'/api/Lobby/sample-training') -Headers $taskAuth}
try{
 foreach($key in $taskSettings.Keys){$taskOld[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,$taskSettings[$key],'Process')}
 $dll=Join-Path $taskServerRoot 'bin/Debug/net8.0/Smart Training Server.dll'
 $taskServer=Start-Process 'dotnet' -ArgumentList ('"'+$dll+'"') -WorkingDirectory $taskServerRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskRun 'server.log') -RedirectStandardError (Join-Path $taskRun 'errors.log')
 $ready=$false
 for($n=0;$n -lt 60;$n++){try{$null=Invoke-RestMethod ($taskApi+'/api/health');$ready=$true;break}catch{Start-Sleep -Milliseconds 100}}
 if(!$ready){throw 'Test API failed to start'}
 $login=Invoke-RestMethod ($taskApi+'/api/Auth/login') -Method Post -ContentType 'application/json' -Body '{"userName":"lobby-observer"}'
 $taskAuth=@{Authorization=('Bearer '+$login.accessToken)}
 for($n=0;$n -lt 2;$n++){
  $folder=Join-Path $taskRun $n.ToString();New-Item -ItemType Directory -Path $folder -Force | Out-Null
  $args='-batchmode -nographics --smarttraining-probe "'+$folder+'" -logFile "'+(Join-Path $folder 'player.log')+'"'
  $taskPlayers+=Start-Process $taskClientExe -ArgumentList $args -WorkingDirectory (Split-Path $taskClientExe -Parent) -WindowStyle Hidden -PassThru
  $null=Wait-Player $n {param($s) $s.processId -gt 0} 'Player startup'
  $null=Send-Player $n 'login' ('lobby-client-'+$n) $taskApi
  $null=Wait-Player $n {param($s) $s.loggedIn -and !$s.busy} 'Player login'
 }
 $null=Send-Player 0 'lobby' 'sample-training'
 $progress=Wait-Player 0 {param($s) $s.canCancelLobby -and $s.lobbyProgress -ne ''} 'Cancelable lobby preparation'
 Check ($progress.busy -and $progress.lobbyProgress.Contains('1/2')) 'cold lobby entry shows preparation progress with active cancel control'
 $null=Send-Player 0 'cancelLobby'
 $cancel=Wait-Player 0 {param($s) !$s.busy -and $s.lobbyProgress -eq '' -and $s.status.Contains('취소')} 'Cold cancellation'
 Check ($cancel.loggedIn -and $cancel.lobbyId -eq '' -and $cancel.pendingLobbyId -eq '' -and !$cancel.canCancelLobby) 'cancel returns logged-in client to lobby list and resets controls'
 $summary=Lobby-Summary
 Check ($summary.userCount -eq 0 -and $summary.pendingUsers -eq 0 -and $summary.serverState -eq 'offline') 'last canceled request leaves no reservation or running lobby worker'
 $null=Send-Player 0 'lobby' 'sample-training'
 $entered=Wait-Player 0 {param($s) !$s.busy -and $s.lobbyId -eq 'sample-training' -and $s.lobbyProgress -eq ''} 'Retry after cancel'
 Check ($entered.loggedIn -and $entered.pendingLobbyId -eq '' -and !$entered.canCancelLobby) 'same client can retry canceled entry and complete normally'
 $null=Send-Player 1 'lobbyPaused' 'sample-training'
 $approved=Wait-Player 1 {param($s) $s.canCancelLobby -and $s.lobbyProgress.Contains('2/2')} 'Approved entry pause'
 Check ($approved.busy -and (Lobby-Summary).userCount -eq 2) 'approval progress is displayed after server commits membership'
 $null=Send-Player 1 'cancelLobby'
 $cancel=Wait-Player 1 {param($s) !$s.busy -and $s.lobbyId -eq '' -and $s.lobbyProgress -eq '' -and $s.status.Contains('취소')} 'Cancellation after server approval'
 Check ($cancel.pendingLobbyId -eq '' -and $cancel.loggedIn) 'late cancel removes already committed lobby membership'
 $summary=Lobby-Summary
 Check ($summary.userCount -eq 1 -and $summary.pendingUsers -eq 0 -and $summary.serverState -eq 'running') 'late cancel preserves other participant and occupied lobby worker'
 $null=Send-Player 1 'lobby' 'failed-test'
 $id=$taskIds[1]
 $failed=Wait-Player 1 {param($s) $s.completed -ge $id -and !$s.busy -and $s.lobbyProgress -eq ''} 'Missing executable failure'
 Check ($failed.loggedIn -and $failed.lobbyId -eq '' -and !$failed.canCancelLobby -and $failed.status.Contains('실행 파일')) 'launch failure clears progress and preserves login with actionable error'
 $null=Send-Player 1 'lobby' 'sample-training'
 $null=Wait-Player 1 {param($s) !$s.busy -and $s.lobbyId -eq 'sample-training'} 'Entry after failed launch'
 Check ((Lobby-Summary).userCount -eq 2) 'client can enter another lobby immediately after failed launch'
 foreach($n in @(1,0)){$null=Send-Player $n 'leaveLobby';$null=Wait-Player $n {param($s) !$s.busy -and $s.lobbyId -eq ''} 'Final lobby departure'}
 $summary=Lobby-Summary
 Check ($summary.userCount -eq 0 -and $summary.pendingUsers -eq 0 -and $summary.serverState -eq 'offline') 'last normal departure still stops lobby worker after cancellation and retries'
 Write-Output ('LOBBY CANCELLATION COMPLETE: '+$taskRun)
}finally{
 foreach($player in $taskPlayers){if(!$player.HasExited){Stop-Process -Id $player.Id -Force -ErrorAction SilentlyContinue}}
 if($taskServer -and !$taskServer.HasExited){Stop-Process -Id $taskServer.Id -Force -ErrorAction SilentlyContinue}
 foreach($key in $taskOld.Keys){[Environment]::SetEnvironmentVariable($key,$taskOld[$key],'Process')}
}
