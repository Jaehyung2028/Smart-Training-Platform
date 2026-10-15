param([string]$ServerProject,[string]$ClientExecutable,[string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
$taskServerRoot=Join-Path $taskRoot 'SmartTrainingServer'
$taskClientExe=Join-Path $taskRoot 'UnityClient/Builds/Client/Smart Training Client.exe'
$taskRun=Join-Path $taskRoot ('lobby-isolation/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
if($ServerProject){$taskServerRoot=[IO.Path]::GetFullPath($ServerProject)}
if($ClientExecutable){$taskClientExe=[IO.Path]::GetFullPath($ClientExecutable)}
if($EvidenceRoot){$taskRun=Join-Path ([IO.Path]::GetFullPath($EvidenceRoot)) (Get-Date -Format 'yyyyMMdd-HHmmss')}
New-Item -ItemType Directory -Path $taskRun -Force | Out-Null
$taskApi='http://127.0.0.1:51430'
$taskServer=$null; $taskPlayers=@(); $taskIds=@(0,0,0); $taskOld=@{}
$taskSettings=@{ASPNETCORE_ENVIRONMENT='Development'; Server__Port='51430'; Room__ServerApiBaseUrl=$taskApi; Room__AdvertisedAddress='127.0.0.1'; SMART_TRAINING_SERVER_PROBE='1'}
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
$taskAuths=@();$taskRoomIds=@();$taskLobbyIds=@('sample-training','safety-training','emergency-training')
function Read-Lobbies { Invoke-RestMethod ($taskApi+'/api/Lobby') -Headers $taskAuth }
function Read-Rooms([string]$id) { Invoke-RestMethod ($taskApi+'/api/Lobby/'+$id+'/rooms') -Headers $taskAuth }
function Post-As([string]$path,$body,$auth){Invoke-RestMethod ($taskApi+$path) -Method Post -Headers $auth -ContentType 'application/json' -Body ($body | ConvertTo-Json -Compress)}
function Worker([string]$id,[bool]$lobby){
 $owned=@(Get-CimInstance Win32_Process -Filter ('ParentProcessId = '+$taskServer.Id))
 $matches=@($owned | Where-Object {$_.CommandLine -match ('-Id '+[regex]::Escape($id)+' ') -and ($_.CommandLine.Contains(' -lobby ') -eq $lobby)})
 if($matches.Count -eq 1){return $matches[0]}
 return $null
}
function Check-Connection([int]$n){
 $expectedRoom=$taskRoomIds[$n];$expectedLobby=$taskLobbyIds[$n]
 $state=Wait-Player $n {param($s) $s.connected -and $s.loggedIn -and $s.roomId -eq $expectedRoom -and $s.lobbyId -eq $expectedLobby -and @($s.participants).Count -eq 1} 'Isolated connection snapshot'
 Check ($state.connected -and $state.loggedIn -and $state.roomId -eq $taskRoomIds[$n] -and $state.lobbyId -eq $taskLobbyIds[$n] -and @($state.participants).Count -eq 1 -and $state.participants[0] -eq $state.userId) ('isolated player '+$n+' retains its room and sole participant')
}
try{
 foreach($key in $taskSettings.Keys){$taskOld[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,$taskSettings[$key],'Process')}
 $dll=Join-Path $taskServerRoot 'bin/Debug/net8.0/Smart Training Server.dll'
 $taskServer=Start-Process 'dotnet' -ArgumentList ('"'+$dll+'"') -WorkingDirectory $taskServerRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskRun 'server.log') -RedirectStandardError (Join-Path $taskRun 'errors.log')
 $ready=$false
 for($n=0;$n -lt 60;$n++){try{$null=Invoke-RestMethod ($taskApi+'/api/health');$ready=$true;break}catch{if($taskServer.HasExited){throw 'Test API exited'};Start-Sleep -Milliseconds 100}}
 if(!$ready){throw 'Test API did not start'}
 $observer=Post-As '/api/Auth/login' @{userName='isolation-observer'} @{}
 $taskAuth=@{Authorization=('Bearer '+$observer.accessToken)}
 $initial=Read-Lobbies
 Check (@($initial).Count -eq 3 -and @($initial|Where-Object {$_.serverState -ne 'offline' -or $_.userCount -ne 0 -or $_.pendingUsers -ne 0 -or $_.roomCount -ne 0}).Count -eq 0) 'three actual catalog lobbies start empty with no running workers'
 for($n=0;$n -lt 3;$n++){
  $login=Post-As '/api/Auth/login' @{userName=('isolation-admin-'+$n)} @{}
  $auth=@{Authorization=('Bearer '+$login.accessToken)};$taskAuths+=,$auth
  $null=Post-As '/api/Lobby/join' @{lobbyId=$taskLobbyIds[$n]} $auth
  $room=Post-As '/api/Room/create' @{lobbyId=$taskLobbyIds[$n];roomName=('isolated-room-'+$n);contentId=$taskLobbyIds[$n];maxUsers=2} $auth
  $taskRoomIds+=$room.room.id
 }
 Check (@($taskRoomIds | Select-Object -Unique).Count -eq 3) 'each catalog lobby creates its own distinct room'
 for($n=0;$n -lt 3;$n++){
  $folder=Join-Path $taskRun $n.ToString();New-Item -ItemType Directory -Path $folder -Force | Out-Null
  $args='-batchmode -nographics --smarttraining-probe "'+$folder+'" -logFile "'+(Join-Path $folder 'player.log')+'"'
  $taskPlayers+=Start-Process $taskClientExe -ArgumentList $args -WorkingDirectory (Split-Path $taskClientExe -Parent) -WindowStyle Hidden -PassThru
 }
 for($n=0;$n -lt 3;$n++){
  $null=Wait-Player $n {param($s) $s.processId -gt 0} 'Player startup'
  $null=Send-Player $n 'login' ('isolation-player-'+$n) $taskApi
  $null=Wait-Player $n {param($s) $s.loggedIn -and !$s.busy} 'Player login'
  $null=Send-Player $n 'lobby' $taskLobbyIds[$n]
 }
 for($n=0;$n -lt 3;$n++){$target=$taskLobbyIds[$n];$null=Wait-Player $n {param($s) !$s.busy -and $s.lobbyId -eq $target} 'Concurrent lobby entry'}
 for($n=0;$n -lt 3;$n++){$null=Send-Player $n 'join' $taskRoomIds[$n] $taskLobbyIds[$n]}
 for($n=0;$n -lt 3;$n++){
  $null=Wait-Player $n {param($s) $s.connected -and !$s.busy -and @($s.participants).Count -eq 1} 'Concurrent room entry'
  $null=Post-As '/api/Auth/logout' @{} $taskAuths[$n]
  Check-Connection $n
 }
 $endpoints=@();$lobbyPids=@();$roomPids=@()
 for($n=0;$n -lt 3;$n++){
  $endpoints+=(Read-Player $n).endpoint
  $lobbyPids+=(Worker $taskLobbyIds[$n] $true).ProcessId
  $roomPids+=(Worker $taskRoomIds[$n] $false).ProcessId
  $list=Read-Rooms $taskLobbyIds[$n]
  Check (@($list).Count -eq 1 -and $list[0].id -eq $taskRoomIds[$n] -and $list[0].lobbyId -eq $taskLobbyIds[$n] -and $list[0].contentId -eq $taskLobbyIds[$n] -and $list[0].connectedUsers -eq 1 -and $list[0].reservedUsers -eq 0) ('lobby '+$n+' API room list contains only its own connected room')
 }
 Check (@($endpoints | Select-Object -Unique).Count -eq 3 -and @($lobbyPids | Select-Object -Unique).Count -eq 3 -and @($roomPids | Select-Object -Unique).Count -eq 3) 'three lobby workers and three Netcode room servers use separate processes and room endpoints'
 for($n=0;$n -lt 3;$n++){
  $target=$taskLobbyIds[$n];$roomId=$taskRoomIds[$n];$userId=(Read-Player $n).userId
  $null=Wait-Player $n {param($s) @($s.rooms).Count -eq 1 -and $s.rooms[0].id -eq $roomId -and @($s.lobbyParticipants).Count -eq 1 -and $s.lobbyParticipants[0].userId -eq $userId -and @($s.lobbies | Where-Object {$_.userCount -ne 1 -or $_.pendingUsers -ne 0 -or $_.roomCount -ne 1 -or $_.serverState -ne 'running'}).Count -eq 0} 'Automatic lobby list and participant refresh'
  Check $true ('player '+$n+' automatically receives correct global counts and only local room and participant lists')
 }
 $pendingLogin=Post-As '/api/Auth/login' @{userName='isolation-pending'} @{}
 $pendingAuth=@{Authorization=('Bearer '+$pendingLogin.accessToken)}
 $null=Post-As '/api/Lobby/join' @{lobbyId=$taskLobbyIds[2]} $pendingAuth
 $crossRejected=$false
 try{$null=Post-As '/api/Room/join' @{lobbyId=$taskLobbyIds[2];roomId=$taskRoomIds[0]} $pendingAuth}catch{$crossRejected=$_.Exception.Response.StatusCode.value__ -eq 400}
 Check $crossRejected 'cross-lobby room join is rejected without opening a reservation'
 $reservation=Post-As '/api/Room/join' @{lobbyId=$taskLobbyIds[2];roomId=$taskRoomIds[2]} $pendingAuth
 $list=Read-Rooms $taskLobbyIds[2]
 Check ($reservation.success -and $list[0].connectedUsers -eq 1 -and $list[0].reservedUsers -eq 1 -and (Read-Rooms $taskLobbyIds[0])[0].reservedUsers -eq 0 -and (Read-Rooms $taskLobbyIds[1])[0].reservedUsers -eq 0) 'one pending Netcode admission appears only in its own lobby room'
 $null=Wait-Player 2 {param($s) $s.rooms[0].reservedUsers -eq 1 -and @($s.lobbyParticipants|Where-Object {$_.connectionState -eq 'reserved'}).Count -eq 1} 'Pending admission automatic refresh'
 Check $true 'pending admission is automatically shown in the matching lobby participant and room lists'
 $null=Post-As '/api/Room/leave' @{lobbyId=$taskLobbyIds[2];roomId=$taskRoomIds[2]} $pendingAuth
 $null=Post-As '/api/Auth/logout' @{} $pendingAuth
 $null=Wait-Player 2 {param($s) $s.rooms[0].reservedUsers -eq 0 -and @($s.lobbyParticipants).Count -eq 1} 'Canceled admission automatic refresh'
 Check $true 'canceling pending admission restores only the matching lobby counts'
 $null=Send-Player 0 'ready' '' '' $true
 $null=Wait-Player 0 {param($s) $s.allReady -and !$s.busy} 'First room ready'
 $null=Send-Player 0 'phase' '' '' $true
 $null=Wait-Player 0 {param($s) $s.running -and !$s.busy} 'First room running'
 Check (!(Read-Player 1).running -and !(Read-Player 1).ready -and !(Read-Player 2).running -and !(Read-Player 2).ready) 'readiness and running phase do not leak to other lobbies'
 $worker=Worker $taskLobbyIds[0] $true
 if(!$worker -or $worker.ProcessId -ne $lobbyPids[0]){throw 'Owned lobby worker identity changed unexpectedly'}
 Stop-Process -Id $worker.ProcessId -Force
 $end=[DateTime]::UtcNow.AddSeconds(45)
 do{
  Start-Sleep -Milliseconds 100
  $replacement=Worker $taskLobbyIds[0] $true
  $snapshot=Read-Lobbies
 }while((!$replacement -or $replacement.ProcessId -eq $lobbyPids[0] -or ($snapshot|Where-Object {$_.id -eq $taskLobbyIds[0]}).serverState -ne 'running') -and [DateTime]::UtcNow -lt $end)
 Check ($replacement -and $replacement.ProcessId -ne $lobbyPids[0]) 'crashed lobby worker restarts while its room is running'
 for($n=0;$n -lt 3;$n++){Check-Connection $n;Check ((Worker $taskRoomIds[$n] $false).ProcessId -eq $roomPids[$n]) ('room server '+$n+' survives another lobby worker crash')}
 Check ((Worker $taskLobbyIds[1] $true).ProcessId -eq $lobbyPids[1] -and (Worker $taskLobbyIds[2] $true).ProcessId -eq $lobbyPids[2] -and (Read-Player 0).running) 'other lobby workers and original running phase survive lobby crash'
 $worker=Worker $taskRoomIds[1] $false
 if(!$worker -or $worker.ProcessId -ne $roomPids[1]){throw 'Owned room worker identity changed unexpectedly'}
 Stop-Process -Id $worker.ProcessId -Force
 $lost=Wait-Player 1 {param($s) !$s.connected -and !$s.busy -and $s.roomId -eq '' -and $s.reconnectRoomId -ne ''} 'Second room crash cleanup'
 Check ($lost.loggedIn -and $lost.lobbyId -eq $taskLobbyIds[1]) 'room crash returns only affected client to its lobby while preserving login'
 foreach($n in @(0,2)){Check-Connection $n;Check ((Worker $taskRoomIds[$n] $false).ProcessId -eq $roomPids[$n]) ('unaffected room server '+$n+' remains unchanged after other room crash')}
 $null=Send-Player 1 'reconnect'
 $null=Wait-Player 1 {param($s) $s.connected -and !$s.busy -and @($s.participants).Count -eq 1 -and $s.reconnectRoomId -eq ''} 'Second room restart and reconnect'
 Check ((Worker $taskRoomIds[1] $false).ProcessId -ne $roomPids[1] -and (Worker $taskLobbyIds[1] $true).ProcessId -eq $lobbyPids[1]) 'affected room restarts independently without replacing its lobby worker'
 $null=Send-Player 0 'leave'
 $null=Wait-Player 0 {param($s) !$s.connected -and !$s.busy -and $s.roomId -eq ''} 'First room departure'
 $null=Send-Player 0 'leaveLobby'
 $null=Wait-Player 0 {param($s) !$s.busy -and $s.lobbyId -eq ''} 'First lobby departure'
 $snapshot=Read-Lobbies;$empty=$snapshot|Where-Object {$_.id -eq $taskLobbyIds[0]}
 Check ($empty.userCount -eq 0 -and $empty.pendingUsers -eq 0 -and $empty.roomCount -eq 0 -and $empty.serverState -eq 'offline' -and !(Worker $taskLobbyIds[0] $true) -and !(Worker $taskRoomIds[0] $false)) 'last departure stops only its lobby and room programs while retaining catalog entry'
 foreach($n in @(1,2)){
  Check-Connection $n
  $null=Wait-Player $n {param($s) @($s.lobbies|Where-Object {$_.id -eq $taskLobbyIds[0] -and $_.userCount -eq 0 -and $_.roomCount -eq 0 -and $_.serverState -eq 'offline'}).Count -eq 1 -and @($s.rooms).Count -eq 1} 'Other clients receive departed lobby counts'
  Check $true ('remaining client '+$n+' automatically sees empty other lobby while keeping its own room list')
 }
 foreach($n in @(0,1,2)){$null=Send-Player $n 'logout';$null=Wait-Player $n {param($s) !$s.loggedIn -and !$s.connected -and !$s.busy} 'Final logout'}
 $snapshot=Read-Lobbies
 Check (@($snapshot).Count -eq 3 -and @($snapshot|Where-Object {$_.serverState -ne 'offline' -or $_.userCount -ne 0 -or $_.pendingUsers -ne 0 -or $_.roomCount -ne 0}).Count -eq 0 -and @(Get-CimInstance Win32_Process -Filter ('ParentProcessId = '+$taskServer.Id) | Where-Object {$_.Name -eq 'Smart Training Room.exe'}).Count -eq 0) 'final logout stops all owned workers and preserves three empty catalog lobbies'
 Write-Output ('LOBBY ISOLATION COMPLETE: '+$taskRun)
}finally{
 foreach($player in $taskPlayers){if(!$player.HasExited){Stop-Process -Id $player.Id -Force -ErrorAction SilentlyContinue}}
 if($taskServer -and !$taskServer.HasExited){Stop-Process -Id $taskServer.Id -Force -ErrorAction SilentlyContinue}
 foreach($key in $taskOld.Keys){[Environment]::SetEnvironmentVariable($key,$taskOld[$key],'Process')}
}
