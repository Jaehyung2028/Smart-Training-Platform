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
try{
 foreach($key in $taskSettings.Keys){$taskOld[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,$taskSettings[$key],'Process')}
 $dll=Join-Path $taskServerRoot 'bin/Debug/net8.0/Smart Training Server.dll'
 $taskServer=Start-Process 'dotnet' -ArgumentList ('"'+$dll+'"') -WorkingDirectory $taskServerRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskRun 'server.log') -RedirectStandardError (Join-Path $taskRun 'server-errors.log')
 $ready=$false
 for($n=0;$n -lt 30;$n++){try{$health=Invoke-RestMethod ($taskApi+'/api/health');$ready=$true;break}catch{Start-Sleep -Milliseconds 200}}
 if(!$ready){throw 'Isolated API failed to start'}
 $login=Invoke-RestMethod ($taskApi+'/api/Auth/login') -Method Post -ContentType 'application/json' -Body (@{userName=('mp-admin-'+[guid]::NewGuid().ToString('N').Substring(0,6))} | ConvertTo-Json)
 $taskAuth=@{Authorization=('Bearer '+$login.accessToken)}
 $catalog=Invoke-RestMethod ($taskApi+'/api/Lobby') -Headers $taskAuth
 $lobby=$catalog[0].id; $contentId=$catalog[0].contentId
 $null=Api-Post '/api/Lobby/join' @{lobbyId=$lobby}
 $room=(Api-Post '/api/Room/create' @{lobbyId=$lobby;roomName='multi-process';contentId=$contentId;maxUsers=2}).room.id
 if(!$room){throw 'Fixture room missing'}
 for($n=0;$n -lt 3;$n++){
  $folder=Join-Path $taskRun $n.ToString();New-Item -ItemType Directory -Path $folder -Force | Out-Null
  $args='-batchmode -nographics --smarttraining-probe "'+$folder+'" -logFile "'+(Join-Path $folder 'player.log')+'"'
  $taskPlayers+=Start-Process $taskClientExe -ArgumentList $args -WorkingDirectory (Split-Path $taskClientExe -Parent) -WindowStyle Hidden -PassThru
 }
 for($n=0;$n -lt 3;$n++){
  $null=Wait-Player $n {param($s) $s.processId -gt 0} 'Probe startup'
  $null=Send-Player $n 'login' ('mp-'+$n+'-'+[guid]::NewGuid().ToString('N').Substring(0,6)) $taskApi
  $null=Wait-Player $n {param($s) $s.loggedIn -and !$s.busy} 'Player login'
  $null=Send-Player $n 'lobby' $lobby
  $id=$taskIds[$n];$null=Wait-Player $n {param($s) $s.completed -ge $id -and !$s.busy} 'Player lobby entry'
 }
 Check (($taskPlayers | Select-Object -ExpandProperty Id -Unique).Count -eq 3) 'three distinct Unity player processes logged in'
 $null=Send-Player 1 'join' $room $lobby
 $joinId=$taskIds[1]
 $null=Wait-Player 1 {param($s) $s.completed -ge $joinId -and $s.canCancel} 'Cancelable cold room admission'
 $null=Send-Player 1 'cancel'
 $cancelId=$taskIds[1]
 $cancelled=Wait-Player 1 {param($s) $s.completed -ge $cancelId -and !$s.busy -and $s.progress -eq ''} 'Independent client cancellation'
 Check ($cancelled.loggedIn -and !$cancelled.connected -and $cancelled.roomId -eq '' -and $cancelled.pendingRoomId -eq '' -and $cancelled.status.Contains('취소')) 'independent player cancels admission and returns to lobby while remaining logged in'
 $cancelRoom=Invoke-RestMethod ($taskApi+'/api/Lobby/'+$lobby+'/rooms/'+$room) -Headers $taskAuth
 Check ($cancelRoom.connectedUsers -eq 0 -and $cancelRoom.reservedUsers -eq 0) 'cancelled player leaves no confirmed or reserved room slot'
 $null=Send-Player 0 'join' $room $lobby
 $owner=Wait-Player 0 {param($s) $s.connected -and !$s.busy -and $s.participants.Count -eq 1 -and $s.masterId -eq $s.userId} 'Owner connection'
 Check ($owner.masterId -eq $owner.userId -and $owner.progress -eq '') 'owner receives confirmed Netcode identity and completed join screen'
 $null=Send-Player 1 'join' $room $lobby
 $null=Send-Player 2 'join' $room $lobby
 $states=@()
 foreach($n in @(1,2)){$id=$taskIds[$n];$states+=Wait-Player $n {param($s) $s.completed -ge $id -and !$s.busy -and $s.progress -eq ''} 'Concurrent admission'}
 Check (@($states | Where-Object {$_.connected}).Count -eq 1) 'only one of two competing real clients enters last free slot'
 $states | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskRun 'admission-result.json') -Encoding UTF8
 $winner=if($states[0].connected){1}else{2};$loser=if($winner -eq 1){2}else{1}
 $rejected=Read-Player $loser
 Check (!$rejected.connected -and $rejected.pendingRoomId -eq '' -and $rejected.status.Contains('가득')) 'rejected client displays capacity error and clears pending admission'
 $owner=Wait-Player 0 {param($s) $s.participants.Count -eq 2} 'Owner replicated roster'
 $member=Wait-Player $winner {param($s) $s.participants.Count -eq 2} 'Member replicated roster'
 $ownerRoster=($owner.participants | Sort-Object) -join ','
 $memberRoster=($member.participants | Sort-Object) -join ','
 Check ($ownerRoster -eq $memberRoster) 'both processes receive identical two-person roster'
 $null=Send-Player 0 'ready' '' '' $true
 $null=Send-Player $winner 'ready' '' '' $true
 foreach($n in @(0,$winner)){$null=Wait-Player $n {param($s) $s.allReady -and !$s.busy} 'Readiness replication'}
 $null=Send-Player 0 'phase' '' '' $true
 foreach($n in @(0,$winner)){$null=Wait-Player $n {param($s) $s.running -and !$s.busy} 'Running phase replication'}
 Check $true 'ready state and running phase reach both independent clients'
 $null=Send-Player $loser 'join' $room $lobby
 $id=$taskIds[$loser];$rejected=Wait-Player $loser {param($s) $s.completed -ge $id -and !$s.busy -and $s.progress -eq ''} 'Running admission rejection'
 Check (!$rejected.connected -and $rejected.pendingRoomId -eq '' -and $rejected.status.Contains('진행')) 'running room rejects third client without residual reservation'
 $null=Send-Player 0 'phase' '' '' $false
 foreach($n in @(0,$winner)){$null=Wait-Player $n {param($s) !$s.running -and !$s.ready -and !$s.busy} 'Waiting and ready reset'}
 Check $true 'returning to waiting resets readiness in both processes'
 $null=Send-Player $winner 'disconnect'
 $disconnected=Wait-Player $winner {param($s) !$s.connected -and !$s.busy -and $s.reconnectRoomId -ne '' -and $s.roomId -eq ''} 'Disconnect cleanup'
 $null=Wait-Player 0 {param($s) $s.participants.Count -eq 1} 'Disconnected roster removal'
 Check ($disconnected.loggedIn -and $disconnected.reconnectRoomId -eq $room) 'transport loss cleans room while preserving login and reconnect target'
 $null=Send-Player $winner 'reconnect'
 $member=Wait-Player $winner {param($s) $s.connected -and !$s.busy -and $s.participants.Count -eq 2 -and $s.reconnectRoomId -eq ''} 'Reconnect'
 $owner=Wait-Player 0 {param($s) $s.participants.Count -eq 2} 'Reconnect roster'
 Check (($member.participants | Select-Object -Unique).Count -eq 2 -and $member.userId -eq $disconnected.userId) 'reconnected process retains identity and appears exactly once'
 Stop-Process -Id $taskPlayers[0].Id -Force
 $member=Wait-Player $winner {param($s) $s.participants.Count -eq 1 -and $s.masterId -eq $s.userId} 'Master crash automatic transfer'
 Check ($member.connected -and $member.loggedIn) 'forced master process termination transfers authority to surviving client'
 $beforeArrival=Invoke-RestMethod ($taskApi+'/api/Lobby/'+$lobby+'/rooms') -Headers $taskAuth
 $beforeArrival | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskRun 'before-arrival-room.json') -Encoding UTF8
 Get-NetUDPEndpoint -LocalPort 6000 -ErrorAction SilentlyContinue | Select-Object LocalAddress,LocalPort,OwningProcess | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskRun 'before-arrival-port.json') -Encoding UTF8
 $null=Send-Player $loser 'join' $room $lobby
 $arrival=Wait-Player $loser {param($s) $s.connected -and !$s.busy -and $s.participants.Count -eq 2} 'Admission after crash'
 $member=Wait-Player $winner {param($s) $s.participants.Count -eq 2} 'Survivor roster after arrival'
 Check ($arrival.masterId -eq $member.userId -and $member.masterId -eq $member.userId) 'new arrival and survivor agree on reassigned master'
 foreach($n in @($winner,$loser)){$null=Send-Player $n 'logout';$null=Wait-Player $n {param($s) !$s.loggedIn -and !$s.connected -and !$s.busy} 'Logout cleanup'}
 $list=Invoke-RestMethod ($taskApi+'/api/Lobby/'+$lobby+'/rooms') -Headers $taskAuth
 Check (@($list).Count -eq 0) 'final logout removes empty room from API list'
 $null=Api-Post '/api/Auth/logout' @{}
 Write-Output ('MULTIPROCESS COMPLETE: evidence saved to '+$taskRun)
}finally{
 foreach($player in $taskPlayers){if(!$player.HasExited){Stop-Process -Id $player.Id -Force -ErrorAction SilentlyContinue}}
 if($taskServer -and !$taskServer.HasExited){Stop-Process -Id $taskServer.Id -Force -ErrorAction SilentlyContinue}
 foreach($key in $taskOld.Keys){[Environment]::SetEnvironmentVariable($key,$taskOld[$key],'Process')}
}
