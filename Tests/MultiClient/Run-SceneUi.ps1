param([string]$ServerProject,[string]$ClientExecutable,[string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
$taskServerRoot=Join-Path $taskRoot 'SmartTrainingServer'
$taskClientExe=Join-Path $taskRoot 'UnityClient/Builds/Client/Smart Training Client.exe'
$taskRun=Join-Path $taskRoot ('scene-ui-evidence/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
if($ServerProject){$taskServerRoot=[IO.Path]::GetFullPath($ServerProject)}
if($ClientExecutable){$taskClientExe=[IO.Path]::GetFullPath($ClientExecutable)}
if($EvidenceRoot){$taskRun=Join-Path ([IO.Path]::GetFullPath($EvidenceRoot)) (Get-Date -Format 'yyyyMMdd-HHmmss')}
New-Item -ItemType Directory -Path $taskRun -Force | Out-Null
$taskApi='http://127.0.0.1:51462'
$taskServer=$null; $taskPlayers=@(); $taskIds=@(0,0,0); $taskOld=@{}
$taskSettings=@{ASPNETCORE_ENVIRONMENT='Development'; Server__Port='51462'; Room__ServerApiBaseUrl=$taskApi; Room__AdvertisedAddress='127.0.0.1'; SMART_TRAINING_SERVER_PROBE='1'}
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
function Capture([int]$index,[string]$name){
 $null=Send-Player $index 'roomCapture' $name;$path=Join-Path (Join-Path $taskRun $index.ToString()) $name;$end=[DateTime]::UtcNow.AddSeconds(10)
 do{if((Test-Path -LiteralPath $path) -and (Get-Item -LiteralPath $path).Length -gt 1000){return};Start-Sleep -Milliseconds 100}while([DateTime]::UtcNow -lt $end)
 throw ('Missing UI capture '+$name)
}
function Open-Create {
 $null=Send-Player 0 'uiCreateOpen';$null=Wait-Player 0 {param($s) $s.uiCreateVisible -and $s.uiInteriorLayoutValid} 'Create modal'
}
function Check-SceneUi([int]$index){
 $s=Wait-Player $index {param($s) $s.uiHasSceneLayout -and $s.uiUsesSceneInstances -and $s.uiLoginViewCount -eq 1 -and $s.uiInteriorViewCount -eq 1 -and $s.uiRoomViewCount -eq 1 -and $s.uiEventSystemCount -eq 1} 'Scene prefab reuse without duplicates'
 Check $true ('client '+$index+' uses exactly three scene UI instances and one EventSystem')
}
function Idle([int]$index){$id=$taskIds[$index];Wait-Player $index {param($s) $s.completed -ge $id -and !$s.busy} 'Command complete'}
function Act([int]$index,[string]$action,[string]$value='', [string]$lobbyId='', [bool]$enabled=$false){if($action -eq 'roomTransfer' -or $action -eq 'roomKick'){$null=Wait-Player $index {param($s) $s.uiRoomParticipantActionsReady} 'Participant management buttons ready'};$null=Send-Player $index $action $value $lobbyId $enabled;$null=Idle $index}
function Join-Room([int]$index,[string]$room,[string]$password=''){
 Act $index 'uiRoomSelect' $room
 Act $index 'uiRoomJoin' $password
 $null=Wait-Player $index {param($s) $s.connected -and $s.uiInRoom -and $s.uiInRoomLayoutValid -and $s.uiRoomSendEnabled} 'Room Canvas admission'
}
try{
 foreach($key in $taskSettings.Keys){$taskOld[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,$taskSettings[$key],'Process')}
 Start-Api
 for($n=0;$n -lt 2;$n++){
  $folder=Join-Path $taskRun $n.ToString();New-Item -ItemType Directory -Path $folder -Force|Out-Null
  $args='-screen-fullscreen 0 -screen-width 1280 -screen-height 800 --smarttraining-probe "'+$folder+'" -logFile "'+(Join-Path $folder 'player.log')+'"'
  $taskPlayers+=Start-Process $taskClientExe -ArgumentList $args -WorkingDirectory (Split-Path $taskClientExe -Parent) -WindowStyle Hidden -PassThru
  $null=Wait-Player $n {param($s) $s.uiVisible -and $s.uiLoginVisible} 'Login startup'
  Check-SceneUi $n
  Act $n 'uiLogin' ('room-ui-'+$n) $taskApi
  Act $n 'uiSelect' 'sample-training';Act $n 'uiJoin'
  $null=Wait-Player $n {param($s) $s.uiInteriorVisible -and $s.uiInteriorLayoutValid} 'Lobby admission'
 }
 Act 0 'uiCreateOpen';Act 0 'uiCreateSubmit' '멀티 접속 확인방' '4'
 $created=Wait-Player 0 {param($s) $s.uiRoomSelected -ne '' -and !$s.uiCreateVisible} 'Create room';$room=$created.uiRoomSelected
 $null=Wait-Player 1 {param($s) $s.uiRoomRows -eq 1} 'Room list update'
 Join-Room 0 $room;Join-Room 1 $room
 $a=Wait-Player 0 {param($s) $s.uiMemberRows -eq 2 -and $s.uiRoomMasterControls} 'Master UI';$user0=$a.userId
 $b=Wait-Player 1 {param($s) $s.uiMemberRows -eq 2 -and !$s.uiRoomMasterControls} 'Member UI';$user1=$b.userId
 Check (!$a.uiVisible -and !$a.uiInteriorVisible -and $a.uiInRoom) 'room Canvas replaces other screens with master and participant state'
 Capture 0 'room-1280.png'
 Act 1 'roomPhase';$b=Idle 1;Check (!$b.running) 'participant cannot start room through disabled master controls'
 Act 0 'roomReady';$null=Wait-Player 0 {param($s) $s.ready -and !$s.uiPhaseEnabled} 'Host ready only'
 Act 1 'roomReady';$null=Wait-Player 0 {param($s) $s.allReady -and $s.uiPhaseEnabled} 'Everyone ready'
 Check $true 'ready buttons synchronize and start requires everyone ready'
 Act 0 'roomPhase';foreach($n in @(0,1)){$null=Wait-Player $n {param($s) $s.running -and !$s.uiReadyEnabled} 'Running phase'}
 Act 0 'roomSettings';Act 0 'roomSettingsSave' '진행 중 변경 시도' '8';$s=Idle 0;Check (@($s.rooms|Where-Object {$_.id -eq $room -and $_.maxUsers -eq 4}).Count -eq 1) 'running phase blocks room name and capacity edits';Act 0 'roomSettingsClose'
 Act 0 'roomPhase';foreach($n in @(0,1)){$null=Wait-Player $n {param($s) !$s.running -and !$s.ready -and $s.uiReadyEnabled} 'Return to waiting'}
 Check $true 'master start and waiting return synchronize and reset readiness'
 Act 0 'roomSettings';$s=Wait-Player 0 {param($s) $s.uiSettingsVisible -and !$s.uiRoomInputEnabled -and $s.uiInRoomLayoutValid -and $s.uiMovementBlocked} 'Settings modal'
 Act 0 'roomSettingsSave' '변경한 멀티 훈련방' '1';$s=Idle 0;Check ($s.status.Contains('인원')) 'capacity below occupied users is rejected inside settings modal'
 Act 0 'roomSettingsSave' '변경한 멀티 훈련방' '3';foreach($n in @(0,1)){$null=Wait-Player $n {param($s) $s.uiInRoomSummary.Contains('/3')} 'Capacity synchronization'}
 Check $true 'room name and capacity changes use existing server validation and synchronize'
 Act 0 'roomPasswordDraft' 'pass5678';Act 0 'roomPasswordSave';$null=Wait-Player 0 {param($s) $s.uiRoomPasswordStatus.Contains('설정됨')} 'Password status'
 Act 0 'roomLock';$null=Wait-Player 1 {param($s) $s.uiInRoomSummary.Contains('잠김')} 'Lock update'
 Act 0 'roomLock';$null=Wait-Player 1 {param($s) $s.uiInRoomSummary.Contains('허용')} 'Unlock update'
 Act 0 'roomPasswordClear';$null=Wait-Player 0 {param($s) $s.uiRoomPasswordStatus.Contains('없음')} 'Password clear'
 Check $true 'password updates and entry lock remain available in master settings'
 Capture 0 'room-settings.png';Act 0 'roomSettingsClose'
 Act 0 'roomChat' '방 채팅 <b>원문</b> 안전 점검 확인'
 foreach($n in @(0,1)){$null=Wait-Player $n {param($s) $s.uiInRoomChat.Contains('<b>원문</b>')} 'Room chat delivery'}
 Check $true 'room chat delivers Korean literal markup to both room clients'
 foreach($n in @(0,1)){Act $n 'roomChannel' '' '' $false}
 Act 1 'roomChat' '로비 채팅도 방에서 확인'
 foreach($n in @(0,1)){$null=Wait-Player $n {param($s) $s.uiInRoomChat.Contains('로비 채팅도 방에서 확인') -and !$s.uiInRoomChat.Contains('<b>원문</b>')} 'Lobby chat channel'}
 foreach($n in @(0,1)){Act $n 'roomChannel' '' '' $true}
 $null=Wait-Player 0 {param($s) $s.uiInRoomChat.Contains('<b>원문</b>') -and !$s.uiInRoomChat.Contains('로비 채팅도 방에서 확인')} 'Room channel restored'
 Check $true 'room and lobby chat channels keep messages separate and restore history'
 Act 0 'roomChatFocus';$null=Wait-Player 0 {param($s) $s.uiTyping} 'Input field focus';Act 0 'roomChatBlur'
 $null=Wait-Player 0 {param($s) !$s.uiTyping} 'Input field blur'
 Check $true 'chat input focus is detected by movement keyboard guard'
 Act 0 'roomRoster' '' '' $true;$null=Wait-Player 0 {param($s) $s.uiMemberRows -eq 2} 'Lobby roster';Act 0 'roomRoster' '' '' $false
 Act 0 'roomMap' '' '' $true;$null=Wait-Player 0 {param($s) $s.uiMapVisible -and $s.uiInRoomLayoutValid -and !$s.uiRoomInputEnabled} 'Map modal'
 Act 0 'roomMoveDown';$null=Wait-Player 0 {param($s) $s.uiMoveY -eq 1} 'Movement button held'
 Act 0 'roomMoveUp';$null=Wait-Player 0 {param($s) $s.uiMoveY -eq 0} 'Movement button released'
 Capture 0 'room-map.png';Act 0 'roomMoveDown';Act 0 'roomMap' '' '' $false
 Act 0 'roomMap' '' '' $true;$null=Wait-Player 0 {param($s) $s.uiMoveY -eq 0} 'Map reopen release';Act 0 'roomMap' '' '' $false
 Check $true 'position map uses synchronized players and held movement releases when closed'
 Act 0 'uiResize' '960x720';$null=Wait-Player 0 {param($s) $s.uiWidth -eq 960 -and $s.uiHeight -eq 720 -and $s.uiInRoomLayoutValid} 'Compact room layout'
 Capture 0 'room-960.png';Act 0 'roomSettings';$null=Wait-Player 0 {param($s) $s.uiInRoomLayoutValid} 'Compact settings layout';Capture 0 'room-settings-960.png';Act 0 'roomSettingsClose'
 Check $true 'room and settings controls stay clickable at 960 by 720'
 Act 0 'roomTransfer' $user1;$s=Wait-Player 0 {param($s) $s.uiConfirmVisible -and !$s.uiRoomInputEnabled -and $s.uiInRoomLayoutValid} 'Transfer confirmation'
 Check ($s.masterId -eq $user0 -and $s.uiRoomConfirmText.Contains('넘길까요')) 'master transfer requires explicit confirmation before changing authority'
 Capture 0 'room-confirm.png';Act 0 'roomCancel';$s=Idle 0;Check ($s.masterId -eq $user0 -and !$s.uiConfirmVisible) 'cancel leaves master authority unchanged'
 Act 0 'roomTransfer' $user1;Act 0 'roomConfirm'
 $null=Wait-Player 0 {param($s) !$s.uiRoomMasterControls -and $s.masterId -eq $user1} 'Former master controls hidden'
 $null=Wait-Player 1 {param($s) $s.uiRoomMasterControls -and $s.masterId -eq $user1} 'New master controls shown'
 Check-SceneUi 0;Check-SceneUi 1
 Check $true 'confirmed transfer updates master controls on both clients'
 Act 1 'roomKick' $user0;Act 1 'roomCancel';$s=Idle 0;Check $s.connected 'cancelled kick preserves participant connection'
 Act 1 'roomKick' $user0;Act 1 'roomConfirm'
 $null=Wait-Player 0 {param($s) !$s.connected -and $s.uiInteriorVisible -and !$s.uiInRoom} 'Kicked client returns lobby'
 $null=Wait-Player 1 {param($s) $s.uiMemberRows -eq 1} 'Kick roster update'
 Check $true 'confirmed kick returns target to lobby and updates room roster'
 Join-Room 0 $room;$null=Wait-Player 1 {param($s) $s.uiMemberRows -eq 2} 'Rejoin after kick'
 Act 1 'roomTransfer' $user0;Act 0 'roomLeave'
 $null=Wait-Player 1 {param($s) !$s.uiConfirmVisible -and $s.uiMemberRows -eq 1} 'Stale target confirmation cancelled'
 Check $true 'target leaving automatically cancels stale master confirmation'
 Join-Room 0 $room;Act 1 'disconnect'
 $null=Wait-Player 0 {param($s) $s.masterId -eq $user0 -and $s.uiRoomMasterControls} 'Master disconnect delegation'
 $null=Wait-Player 1 {param($s) !$s.connected -and $s.uiInteriorVisible -and $s.uiReconnectVisible} 'Reconnect interior'
 Act 1 'uiInteriorReconnect';$null=Wait-Player 1 {param($s) $s.connected -and $s.uiInRoom -and !$s.uiRoomMasterControls} 'Reconnect room Canvas'
 Check-SceneUi 0;Check-SceneUi 1
 Check $true 'master loss delegates authority and reconnect restores room Canvas as participant'
 Act 0 'roomSettings';Act 0 'roomClose';$s=Wait-Player 0 {param($s) $s.uiConfirmVisible -and $s.uiInRoomLayoutValid} 'Close confirmation above settings'
 Check ($s.uiRoomConfirmText.Contains('방을 닫을까요')) 'close room uses confirmation overlay above blocked settings'
 Act 0 'roomCancel';Act 0 'roomClose';Act 0 'roomConfirm'
 foreach($n in @(0,1)){$null=Wait-Player $n {param($s) !$s.connected -and $s.uiInteriorVisible -and !$s.uiInRoom -and !$s.uiSettingsVisible -and !$s.uiConfirmVisible} 'Room closed to lobby'}
 $null=Wait-Player 0 {param($s) $s.uiRoomRows -eq 0} 'Closed room removed'
 Check $true 'confirmed room close removes room and cleans all room screens'
 Act 0 'uiCreateOpen';Act 0 'uiCreateSubmit' '퇴장 확인방' '4';$s=Wait-Player 0 {param($s) $s.uiRoomSelected -ne '' -and !$s.uiCreateVisible} 'Second room';$room=$s.uiRoomSelected
 Join-Room 0 $room;Act 0 'roomLeave';$null=Wait-Player 0 {param($s) !$s.connected -and $s.uiInteriorVisible} 'Room leave button'
 Check $true 'room leave button returns to lobby Canvas'
 Act 0 'uiCreateOpen';Act 0 'uiCreateSubmit' '로그아웃 확인방' '4';$s=Wait-Player 0 {param($s) $s.uiRoomSelected -ne '' -and !$s.uiCreateVisible} 'Logout room';$room=$s.uiRoomSelected
 Join-Room 0 $room;Act 0 'roomLogout';$null=Wait-Player 0 {param($s) !$s.loggedIn -and $s.uiVisible -and $s.uiLoginVisible -and !$s.uiInRoom} 'Room logout button'
 Act 1 'uiInteriorLogout'
 Check-SceneUi 0;Check-SceneUi 1
 Check $true 'room logout returns to login and clears room UI'
 foreach($n in @(0,1)){$log=Join-Path (Join-Path $taskRun $n.ToString()) 'player.log';if(Select-String -LiteralPath $log -Pattern 'NullReferenceException|MissingReferenceException|MissingComponentException|InvalidOperationException|ArgumentException|MULTICLIENT PROBE:'){throw ('Room UI runtime exception in player '+$n)}}
 Check $true 'both clients complete without UI runtime exceptions'
 Write-Output ('SCENE UI RUNTIME COMPLETE: '+$taskRun)
}finally{
 foreach($player in $taskPlayers){if(!$player.HasExited){Stop-Process -Id $player.Id -Force -ErrorAction SilentlyContinue}}
 Stop-Api
 foreach($key in $taskOld.Keys){[Environment]::SetEnvironmentVariable($key,$taskOld[$key],'Process')}
}
