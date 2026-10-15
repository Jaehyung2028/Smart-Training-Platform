param([string]$ServerProject,[string]$ClientExecutable,[string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
$taskServerRoot=Join-Path $taskRoot 'SmartTrainingServer'
$taskClientExe=Join-Path $taskRoot 'UnityClient/Builds/Client/Smart Training Client.exe'
$taskRun=Join-Path $taskRoot ('lobby-failure/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
if($ServerProject){$taskServerRoot=[IO.Path]::GetFullPath($ServerProject)}
if($ClientExecutable){$taskClientExe=[IO.Path]::GetFullPath($ClientExecutable)}
if($EvidenceRoot){$taskRun=Join-Path ([IO.Path]::GetFullPath($EvidenceRoot)) (Get-Date -Format 'yyyyMMdd-HHmmss')}
New-Item -ItemType Directory -Path $taskRun -Force | Out-Null
$taskApi='http://127.0.0.1:51435'
$taskServer=$null; $taskPlayers=@(); $taskIds=@(0,0,0); $taskOld=@{}
$taskSettings=@{ASPNETCORE_ENVIRONMENT='Development'; Server__Port='51435'; Room__ServerApiBaseUrl=$taskApi; Room__AdvertisedAddress='127.0.0.1'; SMART_TRAINING_SERVER_PROBE='1'}
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
$taskSettings['Lobby__ServerReadyTimeoutSeconds']='5'
$taskFixture=Join-Path $taskRun 'retry-worker'
$taskFixtureExe=Join-Path $taskFixture 'Smart Training Room.exe'
$taskRuntime=Join-Path $taskServerRoot 'bin/Debug/net8.0/RoomServer'
$taskSilentProject=Join-Path $taskRoot 'SilentLobbyWorker/SilentLobbyWorker.csproj'
if(!(Test-Path -LiteralPath $taskSilentProject)){$taskSilentProject=Join-Path $taskRoot '../Fixtures/SilentLobbyWorker/SilentLobbyWorker.csproj'}
$taskSilentOutput=Join-Path $taskRun 'silent-worker'
& dotnet build $taskSilentProject --ignore-failed-sources --output $taskSilentOutput *> (Join-Path $taskRun 'silent-worker-build.log')
if($LASTEXITCODE -ne 0){throw 'Silent lobby fixture build failed'}
$taskSilentExe=Join-Path $taskSilentOutput 'SilentLobbyWorker.exe'
foreach($file in Get-ChildItem -LiteralPath $taskRuntime -File -Recurse | Where-Object {$_.Extension -ne '.log' -and $_.FullName -ne (Join-Path $taskRuntime 'Smart Training Room.exe')}){
 $relative=$file.FullName.Substring($taskRuntime.Length).TrimStart('\','/')
 $target=Join-Path $taskFixture $relative
 New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
 Copy-Item -LiteralPath $file.FullName -Destination $target
}
@(
 @{rootContent='healthy-test';id=1;title='Healthy';children=@()},
 @{rootContent='retry-test';id=10;title='Retry';children=@();lobbyServerExecutablePath=$taskFixtureExe},
 @{rootContent='timeout-test';id=20;title='Timeout';children=@();lobbyServerExecutablePath=$taskSilentExe},
 @{rootContent='dropped-test';id=30;title='Dropped';children=@();lobbyServerExecutablePath=$taskSilentExe}
) | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskRun 'catalog.json') -Encoding UTF8
function Summary([string]$id){Invoke-RestMethod ($taskApi+'/api/Lobby/'+$id) -Headers $taskAuth}
function Wait-Failure([string]$id,[string]$fragment){Wait-Player 0 {param($s) !$s.busy -and $s.lobbyProgress -eq '' -and $s.lobbyRetryId -eq $id -and $s.lobbyError.Contains($fragment)} 'Specific retryable failure'}
try{
 foreach($key in $taskSettings.Keys){$taskOld[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,$taskSettings[$key],'Process')}
 $dll=Join-Path $taskServerRoot 'bin/Debug/net8.0/Smart Training Server.dll'
 $taskServer=Start-Process 'dotnet' -ArgumentList ('"'+$dll+'"') -WorkingDirectory $taskServerRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskRun 'server.log') -RedirectStandardError (Join-Path $taskRun 'errors.log')
 $ready=$false
 for($n=0;$n -lt 60;$n++){try{$null=Invoke-RestMethod ($taskApi+'/api/health');$ready=$true;break}catch{Start-Sleep -Milliseconds 100}}
 if(!$ready){throw 'Failure test API did not start'}
 $login=Invoke-RestMethod ($taskApi+'/api/Auth/login') -Method Post -ContentType 'application/json' -Body '{"userName":"failure-observer"}'
 $taskAuth=@{Authorization=('Bearer '+$login.accessToken)}
 for($n=0;$n -lt 2;$n++){
  $folder=Join-Path $taskRun $n.ToString();New-Item -ItemType Directory -Path $folder -Force | Out-Null
  $args='-batchmode -nographics --smarttraining-probe "'+$folder+'" -logFile "'+(Join-Path $folder 'player.log')+'"'
  $taskPlayers+=Start-Process $taskClientExe -ArgumentList $args -WorkingDirectory (Split-Path $taskClientExe -Parent) -WindowStyle Hidden -PassThru
  $null=Wait-Player $n {param($s) $s.processId -gt 0} 'Player startup'
  $null=Send-Player $n 'login' ('failure-player-'+$n) $taskApi
  $null=Wait-Player $n {param($s) $s.loggedIn -and !$s.busy} 'Login'
 }
 $null=Send-Player 1 'lobby' 'healthy-test'
 $null=Wait-Player 1 {param($s) !$s.busy -and $s.lobbyId -eq 'healthy-test'} 'Healthy control lobby'
 $null=Send-Player 0 'lobby' 'retry-test'
 $failed=Wait-Failure 'retry-test' '실행 파일'
 Check ($failed.loggedIn -and $failed.lobbyId -eq '' -and $failed.pendingLobbyId -eq '' -and !$failed.canCancelLobby) 'missing executable displays specific failure with retry target and preserves login'
 Check (!$failed.lobbyError.Contains($taskFixture) -and (Summary 'retry-test').pendingUsers -eq 0 -and (Summary 'retry-test').userCount -eq 0) 'failure does not expose local paths or retain admission state'
 $null=Send-Player 0 'retryLobby'
 $id=$taskIds[0];$failed=Wait-Player 0 {param($s) $s.completed -ge $id -and !$s.busy -and $s.lobbyRetryId -eq 'retry-test'} 'Repeated failure'
 Check ($failed.lobbyError.Contains('실행 파일') -and (Summary 'retry-test').pendingUsers -eq 0) 'retrying before repair shows same clear error without duplicate reservations'
 Copy-Item -LiteralPath (Join-Path $taskRuntime 'Smart Training Room.exe') -Destination $taskFixtureExe
 $null=Send-Player 0 'retryLobby'
 $entered=Wait-Player 0 {param($s) !$s.busy -and $s.lobbyId -eq 'retry-test' -and $s.lobbyRetryId -eq '' -and $s.lobbyError -eq ''} 'Retry after fixture repair'
 Check ($entered.loggedIn -and $entered.pendingLobbyId -eq '' -and (Summary 'retry-test').userCount -eq 1) 'same lobby retry succeeds after repair and clears failure panel'
 $null=Send-Player 0 'leaveLobby';$null=Wait-Player 0 {param($s) !$s.busy -and $s.lobbyId -eq ''} 'Leave repaired lobby'
 $null=Send-Player 0 'lobby' 'timeout-test'
 $failed=Wait-Failure 'timeout-test' '시간이 초과'
 Check ($failed.loggedIn -and $failed.pendingLobbyId -eq '' -and (Summary 'timeout-test').pendingUsers -eq 0 -and (Summary 'timeout-test').serverState -eq 'offline') 'readiness timeout has distinct retryable message and stops empty worker'
 $null=Send-Player 0 'lobby' 'dropped-test'
 $null=Wait-Player 0 {param($s) $s.canCancelLobby -and $s.lobbyProgress -ne ''} 'Dropped startup preparation'
 $deadline=[DateTime]::UtcNow.AddSeconds(3)
 do{$workers=@(Get-CimInstance Win32_Process -Filter ('ParentProcessId = '+$taskServer.Id) | Where-Object {$_.CommandLine -match '-Id dropped-test '});if($workers.Count -eq 1){break};Start-Sleep -Milliseconds 50}while([DateTime]::UtcNow -lt $deadline)
 if($workers.Count -ne 1){throw 'Owned dropped fixture missing'}
 Stop-Process -Id $workers[0].ProcessId -Force
 $failed=Wait-Failure 'dropped-test' '연결이 끊겼'
 Check ($failed.loggedIn -and $failed.pendingLobbyId -eq '' -and (Summary 'dropped-test').pendingUsers -eq 0) 'startup process loss has connection-specific message and cleans pending admission'
 $healthy=Wait-Player 1 {param($s) !$s.busy -and $s.loggedIn -and $s.lobbyId -eq 'healthy-test'} 'Healthy lobby remains occupied'
 Check ((Summary 'healthy-test').userCount -eq 1 -and (Summary 'healthy-test').serverState -eq 'running') 'failed lobby startup and retries preserve another occupied lobby'
 $null=Send-Player 0 'lobby' 'retry-test'
 $null=Wait-Player 0 {param($s) !$s.busy -and $s.lobbyId -eq 'retry-test' -and $s.lobbyError -eq '' -and $s.lobbyRetryId -eq ''} 'Another lobby after failures'
 Check $true 'choosing another successful lobby clears previous failure and retry target'
 foreach($n in @(0,1)){$null=Send-Player $n 'logout';$null=Wait-Player $n {param($s) !$s.loggedIn -and !$s.busy -and $s.lobbyError -eq '' -and $s.lobbyRetryId -eq ''} 'Logout and failure reset'}
 Check $true 'logout clears failure and retry state for subsequent sessions'
 Write-Output ('LOBBY FAILURE COMPLETE: '+$taskRun)
}finally{
 foreach($player in $taskPlayers){if(!$player.HasExited){Stop-Process -Id $player.Id -Force -ErrorAction SilentlyContinue}}
 if($taskServer -and !$taskServer.HasExited){Stop-Process -Id $taskServer.Id -Force -ErrorAction SilentlyContinue}
 foreach($key in $taskOld.Keys){[Environment]::SetEnvironmentVariable($key,$taskOld[$key],'Process')}
}
