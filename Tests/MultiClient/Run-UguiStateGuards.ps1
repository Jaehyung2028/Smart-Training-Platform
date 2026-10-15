param([Parameter(Mandatory=$true)][string]$ServerProject,[Parameter(Mandatory=$true)][string]$ClientExecutable,[Parameter(Mandatory=$true)][string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
$taskServerRoot=Join-Path $taskRoot 'SmartTrainingServer'
$taskClientExe=Join-Path $taskRoot 'UnityClient/Builds/Client/Smart Training Client.exe'
$taskRun=Join-Path $taskRoot ('ui-state-evidence/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
if($ServerProject){$taskServerRoot=[IO.Path]::GetFullPath($ServerProject)}
if($ClientExecutable){$taskClientExe=[IO.Path]::GetFullPath($ClientExecutable)}
if($EvidenceRoot){$taskRun=Join-Path ([IO.Path]::GetFullPath($EvidenceRoot)) (Get-Date -Format 'yyyyMMdd-HHmmss')}
New-Item -ItemType Directory -Path $taskRun -Force | Out-Null
$taskApi='http://127.0.0.1:51464'
$taskServer=$null; $taskPlayers=@(); $taskIds=@(0,0,0); $taskOld=@{}
$taskSettings=@{ASPNETCORE_ENVIRONMENT='Development'; Server__Port='51464'; Room__ServerApiBaseUrl=$taskApi; Room__AdvertisedAddress='127.0.0.1'; SMART_TRAINING_SERVER_PROBE='1'}
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
$taskSettings['Logging__LogLevel__Microsoft.AspNetCore.Hosting.Diagnostics']='Information'
function Idle([int]$index){$id=$taskIds[$index];Wait-Player $index {param($s) $s.completed -ge $id -and !$s.busy} 'Command completion'}
function Act([int]$index,[string]$action,[string]$value='', [string]$lobbyId='', [bool]$enabled=$false){
 if($action -eq 'roomTransfer' -or $action -eq 'roomKick'){$null=Wait-Player $index {param($s) $s.uiRoomParticipantActionsReady} 'Participant controls enabled'}
 $null=Send-Player $index $action $value $lobbyId $enabled;$null=Idle $index
}
function Count-Post([string]$path){
 $log=Join-Path $taskRun 'server-1.log'
 return @(Select-String -LiteralPath $log -Pattern ('Request starting .* POST [^\r\n]*'+[regex]::Escape($path)+'(?:\s|$)')).Count
}
function Check-OnePost([int]$before,[string]$path,[string]$name){Check (((Count-Post $path)-$before) -eq 1) $name}
function Check-Clean([int]$index,[string]$name){
 $s=Wait-Player $index {param($s) !$s.busy -and $s.auditPasswordsEmpty -and $s.auditPasswordWidgetsEmpty -and $s.auditFocusClear -and !$s.uiCreateVisible -and !$s.uiRoomProgressVisible -and !$s.uiProgressVisible -and !$s.uiSettingsVisible -and !$s.uiConfirmVisible -and !$s.uiMapVisible} $name
 Check $true $name
}
try{
 foreach($key in $taskSettings.Keys){$taskOld[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,$taskSettings[$key],'Process')}
 Start-Api
 for($n=0;$n -lt 2;$n++){
  $folder=Join-Path $taskRun $n.ToString();New-Item -ItemType Directory -Path $folder -Force|Out-Null
  $args='-screen-fullscreen 0 -screen-width 1280 -screen-height 800 --smarttraining-probe "'+$folder+'" -logFile "'+(Join-Path $folder 'player.log')+'"'
  $taskPlayers+=Start-Process $taskClientExe -ArgumentList $args -WorkingDirectory (Split-Path $taskClientExe -Parent) -WindowStyle Hidden -PassThru
  $null=Wait-Player $n {param($s) $s.uiVisible -and $s.uiLoginVisible} 'Login startup'
  $before=Count-Post '/api/Auth/login'
  Act $n 'auditBurstLogin' ('ui-state-'+$n) $taskApi
  $s=Wait-Player $n {param($s) $s.loggedIn -and !$s.busy -and $s.uiRows -eq 3} 'Login after 100 clicks'
  Check-OnePost $before '/api/Auth/login' ('100 login clicks send exactly one request for client '+$n)
  Check ($s.auditImmediateBlocked -and $s.auditCaption.Contains('로그인하고')) 'login locks immediately and names the current operation'
  Act $n 'uiSelect' 'sample-training';Act $n 'uiJoin'
  $null=Wait-Player $n {param($s) $s.uiInteriorVisible -and $s.uiChatEnabled} 'Lobby admission'
 }
 $before=Count-Post '/api/Room/create';Act 0 'uiCreateOpen';Act 0 'auditBurstCreate' '중복 클릭 확인방'
 $s=Wait-Player 0 {param($s) !$s.busy -and !$s.uiCreateVisible -and $s.uiRoomSelected -ne ''} 'Protected room creation';$room=$s.uiRoomSelected
 Check-OnePost $before '/api/Room/create' '100 create clicks create one protected room with one HTTP request'
 Check ($s.auditImmediateBlocked -and $s.auditCaption.Contains('방을 생성')) 'create locks immediately and shows creation progress'
 $before=Count-Post '/api/Room/join';Act 0 'auditBurstJoin' 'pass1234'
 $a=Wait-Player 0 {param($s) $s.connected -and $s.uiInRoom -and !$s.busy} 'Owner joins';$user0=$a.userId
 Check-OnePost $before '/api/Room/join' '100 join clicks reserve and connect once'
 Check ($a.auditImmediateBlocked -and $a.auditCaption.Contains('방에 입장')) 'join locks immediately and names admission progress'
 $null=Wait-Player 1 {param($s) $s.uiRoomRows -eq 1} 'Peer receives room';Act 1 'uiRoomSelect' $room;Act 1 'uiRoomJoin' 'pass1234'
 $b=Wait-Player 1 {param($s) $s.connected -and @($s.participants).Count -eq 2 -and $s.uiRoomSendEnabled} 'Peer joins';$user1=$b.userId
 Check-Clean 0 'room entry clears hidden admission windows and password widgets'
 Act 0 'roomSettings';$before=Count-Post '/api/Room/settings';Act 0 'auditBurstSettings' '설정 저장 확인방'
 $s=Wait-Player 0 {param($s) !$s.busy -and $s.status.Contains('변경했습니다')} 'Settings saved'
 Check-OnePost $before '/api/Room/settings' '100 settings saves send one validated update'
 Check ($s.auditImmediateBlocked -and $s.auditCaption.Contains('방 설정을 저장')) 'settings lock immediately and show saving progress'
 Act 0 'roomSettingsClose';Act 0 'auditBurstReady';$s=Wait-Player 0 {param($s) $s.ready -and !$s.busy} 'Ready after burst'
 Check ($s.auditImmediateBlocked -and $s.auditCaption.Contains('준비 상태')) '100 ready clicks keep one transition and block repeated toggling'
 Act 1 'roomReady';$null=Wait-Player 0 {param($s) $s.allReady -and $s.uiPhaseEnabled} 'Everyone ready'
 Act 0 'auditBurstPhase';foreach($n in @(0,1)){$null=Wait-Player $n {param($s) $s.running -and !$s.busy} 'Room running'}
 Check $true '100 phase clicks start once without toggling back'
 Act 0 'roomPhase';$null=Wait-Player 0 {param($s) !$s.running -and !$s.busy} 'Room waiting'
 $before=Count-Post '/api/Room/leave';Act 0 'auditBurstLeave';$null=Wait-Player 0 {param($s) !$s.connected -and $s.uiInteriorVisible -and !$s.busy} 'Leave after burst'
 Check-OnePost $before '/api/Room/leave' '100 leave clicks send one cleanup request'
 Check-Clean 0 'room exit clears passwords modal state and keyboard focus'
 Act 0 'uiRoomSelect' $room;$before=Count-Post '/api/Room/join';$null=Send-Player 0 'auditBurstJoinPaused' 'pass1234'
 $s=Wait-Player 0 {param($s) $s.busy -and $s.canCancel -and $s.uiRoomProgressVisible} 'Paused admission'
 Check ($s.auditImmediateBlocked -and $s.auditBackgroundBlocked) 'admission blocks background interaction while preserving cancellation'
 $createBefore=Count-Post '/api/Room/create';$logoutBefore=Count-Post '/api/Auth/logout';$null=Send-Player 0 'auditAttemptBusy';$id=$taskIds[0]
 $null=Wait-Player 0 {param($s) $s.completed -ge $id -and $s.busy} 'Blocked callbacks while busy'
 Check ((Count-Post '/api/Room/create') -eq $createBefore -and (Count-Post '/api/Auth/logout') -eq $logoutBefore) 'callbacks behind busy UI cannot create a room or logout'
 $leaveBefore=Count-Post '/api/Room/leave';$null=Send-Player 0 'auditBurstCancel'
 $s=Wait-Player 0 {param($s) !$s.busy -and !$s.connected -and $s.pendingRoomId -eq '' -and $s.uiInteriorVisible} 'Cancelled admission cleanup'
 Check ($s.auditImmediateBlocked -and $s.auditCaption.Contains('입장 취소')) '100 cancel clicks lock immediately and show cancellation cleanup'
 Check-OnePost $before '/api/Room/join' 'paused burst admission still issues only one reservation request'
 Check-OnePost $leaveBefore '/api/Room/leave' '100 cancellation clicks send one cleanup request'
 $null=Wait-Player 1 {param($s) $s.connected -and @($s.participants).Count -eq 1} 'Existing peer preserved'
 Check-Clean 0 'cancelled entry clears sensitive inputs without disconnecting the other participant'
 Act 0 'uiRoomSelect' $room;Act 0 'uiRoomJoin' 'pass1234';$null=Wait-Player 0 {param($s) $s.connected -and $s.uiInRoom} 'Rejoin after cancellation'
 $null=Wait-Player 1 {param($s) $s.uiRoomMasterControls -and $s.uiMemberRows -eq 2} 'Peer is now master'
 Act 1 'auditRoomDraft' '전환 시 정리할 방 채팅 초안';Act 1 'roomSettings';Act 1 'roomPasswordDraft' 'unsaved9876';Act 1 'auditFocusPassword' 'room'
 $null=Wait-Player 1 {param($s) $s.uiSettingsVisible -and !$s.auditPasswordsEmpty -and !$s.auditFocusClear} 'Unsubmitted settings and password focus'
 Act 1 'disconnect';$null=Wait-Player 1 {param($s) !$s.connected -and $s.uiInteriorVisible -and $s.uiReconnectVisible -and !$s.busy} 'Disconnected to lobby'
 Check-Clean 1 'transport loss closes settings clears passwords and removes field focus'
 Act 1 'uiInteriorReconnect' 'pass1234';$null=Wait-Player 1 {param($s) $s.connected -and $s.uiInRoom -and !$s.busy} 'Protected reconnect'
 Check-Clean 1 'password-protected reconnect survives cleanup and leaves no password draft'
 $null=Wait-Player 0 {param($s) $s.uiRoomMasterControls -and $s.uiRoomParticipantActionsReady} 'Delegated master controls'
 Act 0 'roomTransfer' $user1;Act 0 'roomConfirm';$null=Wait-Player 1 {param($s) $s.uiRoomMasterControls -and $s.uiRoomParticipantActionsReady} 'Peer master after transfer'
 Act 0 'roomMap' '' '' $true;$null=Wait-Player 0 {param($s) $s.uiMapVisible} 'Map open before kick'
 Act 1 'roomKick' $user0;Act 1 'roomConfirm';$null=Wait-Player 0 {param($s) !$s.connected -and $s.uiInteriorVisible -and !$s.busy} 'Kicked to lobby'
 Check-Clean 0 'kick closes open map and clears room passwords and focus'
 $s=Idle 0;Check ($s.reconnectRoomId -eq '') 'kick does not leave an automatic reconnect target'
 Act 0 'uiCreateOpen';Act 0 'auditCreateDraft' 'draft9876';Act 0 'auditFocusPassword' 'create'
 $null=Wait-Player 0 {param($s) $s.uiCreateVisible -and !$s.auditPasswordWidgetsEmpty -and !$s.auditFocusClear} 'Create draft before auth revocation'
 Act 0 'auditRevokeSession';$null=Wait-Player 0 {param($s) !$s.loggedIn -and !$s.busy -and $s.uiVisible -and $s.uiLoginVisible} 'Revoked session returns to login'
 Check-Clean 0 'auth revocation clears open create window passwords and selected UI'
 $before=Count-Post '/api/Auth/logout';Act 1 'auditBurstRoomLogout';$null=Wait-Player 1 {param($s) !$s.loggedIn -and !$s.busy -and $s.uiLoginVisible} 'Room logout after burst'
 Check-OnePost $before '/api/Auth/logout' '100 room logout clicks send one logout request'
 Check-Clean 1 'room logout clears hidden windows password widgets and focus'
 Act 0 'auditBurstLogin' 'ui-state-0' $taskApi;$s=Wait-Player 0 {param($s) $s.loggedIn -and !$s.busy -and $s.uiRows -eq 3} 'Fresh login after revocation'
 Check ($s.lobbyId -eq '' -and $s.roomId -eq '' -and $s.pendingRoomId -eq '' -and $s.uiRoomSelected -eq '') 'fresh login cannot reuse old room membership or selection'
 Act 0 'uiSelect' 'sample-training';Act 0 'uiJoin';$null=Wait-Player 0 {param($s) $s.uiInteriorVisible} 'Lobby for departure cleanup'
 Act 0 'uiCreateOpen';Act 0 'uiCreateSubmit' '로비 퇴장 확인방' '4' $true
 $s=Wait-Player 0 {param($s) !$s.uiCreateVisible -and $s.uiRoomSelected -ne ''} 'Protected room for unsubmitted password'
 Act 0 'uiRoomSelect' $s.uiRoomSelected;Act 0 'auditJoinDraft' 'unsubmitted9876';Act 0 'auditFocusPassword' 'join'
 $null=Wait-Player 0 {param($s) !$s.auditPasswordsEmpty -and !$s.auditFocusClear} 'Unsubmitted admission password'
 Act 0 'uiLobbyLeave';$null=Wait-Player 0 {param($s) $s.lobbyId -eq '' -and $s.uiVisible -and !$s.uiInteriorVisible} 'Lobby exit'
 Check-Clean 0 'lobby exit clears unsubmitted admission passwords and hidden selection focus'
 Act 0 'uiLogout'
 foreach($n in @(0,1)){$log=Join-Path (Join-Path $taskRun $n.ToString()) 'player.log';if(Select-String -LiteralPath $log -Pattern 'NullReferenceException|MissingReferenceException|MissingComponentException|InvalidOperationException|ArgumentException|MULTICLIENT PROBE:'){throw ('UI state exception in player '+$n)}}
 Check $true 'both clients finish without UI runtime exceptions'
 Write-Output ('UI STATE COMPLETE: '+$taskRun)
}finally{
 foreach($player in $taskPlayers){if(!$player.HasExited){Stop-Process -Id $player.Id -Force -ErrorAction SilentlyContinue}}
 Stop-Api
 foreach($key in $taskOld.Keys){[Environment]::SetEnvironmentVariable($key,$taskOld[$key],'Process')}
}
