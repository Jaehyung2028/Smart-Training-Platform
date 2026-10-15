param([string]$ServerProject,[string]$ClientExecutable,[string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
$taskServerRoot=Join-Path $taskRoot 'SmartTrainingServer'
$taskClientExe=Join-Path $taskRoot 'UnityClient/Builds/Client/Smart Training Client.exe'
$taskRun=Join-Path $taskRoot ('interior-evidence/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
if($ServerProject){$taskServerRoot=[IO.Path]::GetFullPath($ServerProject)}
if($ClientExecutable){$taskClientExe=[IO.Path]::GetFullPath($ClientExecutable)}
if($EvidenceRoot){$taskRun=Join-Path ([IO.Path]::GetFullPath($EvidenceRoot)) (Get-Date -Format 'yyyyMMdd-HHmmss')}
New-Item -ItemType Directory -Path $taskRun -Force | Out-Null
$taskApi='http://127.0.0.1:51458'
$taskServer=$null; $taskPlayers=@(); $taskIds=@(0,0,0); $taskOld=@{}
$taskSettings=@{ASPNETCORE_ENVIRONMENT='Development'; Server__Port='51458'; Room__ServerApiBaseUrl=$taskApi; Room__AdvertisedAddress='127.0.0.1'; SMART_TRAINING_SERVER_PROBE='1'}
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
 $null=Send-Player $index 'uiInteriorCapture' $name;$path=Join-Path (Join-Path $taskRun $index.ToString()) $name;$end=[DateTime]::UtcNow.AddSeconds(10)
 do{if((Test-Path -LiteralPath $path) -and (Get-Item -LiteralPath $path).Length -gt 1000){return};Start-Sleep -Milliseconds 100}while([DateTime]::UtcNow -lt $end)
 throw ('Missing UI capture '+$name)
}
function Open-Create {
 $null=Send-Player 0 'uiCreateOpen';$null=Wait-Player 0 {param($s) $s.uiCreateVisible -and $s.uiInteriorLayoutValid} 'Create modal'
}
try{
 foreach($key in $taskSettings.Keys){$taskOld[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,$taskSettings[$key],'Process')}
 Start-Api
 for($n=0;$n -lt 2;$n++){
  $folder=Join-Path $taskRun $n.ToString();New-Item -ItemType Directory -Path $folder -Force|Out-Null
  $args='-screen-fullscreen 0 -screen-width 1280 -screen-height 800 --smarttraining-probe "'+$folder+'" -logFile "'+(Join-Path $folder 'player.log')+'"'
  $taskPlayers+=Start-Process $taskClientExe -ArgumentList $args -WorkingDirectory (Split-Path $taskClientExe -Parent) -WindowStyle Hidden -PassThru
  $null=Wait-Player $n {param($s) $s.uiVisible -and $s.uiLoginVisible} 'Login UI startup'
  $null=Send-Player $n 'uiLogin' ('interior-user-'+$n) $taskApi;$null=Wait-Player $n {param($s) $s.loggedIn -and !$s.busy -and $s.uiRows -eq 3} 'uGUI login'
  $null=Send-Player $n 'uiSelect' 'sample-training';$null=Wait-Player $n {param($s) !$s.busy -and $s.uiEnterEnabled} 'Lobby choice'
  $null=Send-Player $n 'uiJoin';$null=Wait-Player $n {param($s) !$s.busy -and $s.uiInteriorVisible -and $s.uiInteriorLayoutValid -and $s.uiChatEnabled} 'Interior Canvas admission'
 }
 $null=Wait-Player 0 {param($s) $s.uiParticipantRows -eq 2} 'Two participants'
 Check $true 'login and catalog uGUI hand off to interior Canvas with live participant rows'
 $null=Send-Player 0 'uiChatDraft' '작성 중인 메시지'
 $reason=('기존 참가자는 계속 이용할 수 있습니다. '+('운영 안내와 안전 점검 사유를 확인해주세요. '*5)).Trim()
 $null=Mode $true $reason 0
 $null=Wait-Player 0 {param($s) !$s.busy -and $s.uiOperationNotice.Contains($reason) -and $s.uiInteriorLayoutValid -and $s.uiChatDraft -eq '작성 중인 메시지'} 'Long maintenance notice and retained draft'
 $null=Mode $false '운영 재개' 1
 $body=@{mode='scheduled';reason='예약 종료 안내 검증';atUtc=[DateTimeOffset]::UtcNow.AddMinutes(10).ToString('o');expectedRevision=2}|ConvertTo-Json -Compress
 $null=Invoke-RestMethod ($taskApi+'/api/operations/lobbies/sample-training/mode') -Method Post -Headers $taskOpsHeaders -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
 $null=Wait-Player 0 {param($s) !$s.busy -and $s.uiOperationNotice.Contains('종료 예정') -and $s.uiOperationNotice.Contains('남은') -and $s.uiChatDraft -eq '작성 중인 메시지'} 'Scheduled notice'
 Capture 0 'operating-notice.png'
 $null=Mode $false '운영 재개' 3
 $null=Wait-Player 0 {param($s) !$s.busy -and $s.uiOperationNotice -eq '' -and $s.uiChatDraft -eq '작성 중인 메시지'} 'Resume notice'
 Check $true 'maintenance and scheduled countdown update without clearing chat draft'
 $chat='안전 확인 <b>원문 표시</b> '+('훈련 절차와 안전 수칙을 확인합니다. '*4)
 $null=Send-Player 0 'uiLobbyChat' $chat
 foreach($n in @(0,1)){$null=Wait-Player $n {param($s) !$s.busy -and $s.uiChatText.Contains('<b>원문 표시</b>') -and $s.uiChatLines -eq 1} 'Chat delivery'}
 Check $true 'lobby chat button delivers Korean text and literal markup to both clients'
 Open-Create
 $null=Send-Player 0 'uiCreateSubmit' '비밀번호 훈련방' '0' $true
 $null=Wait-Player 0 {param($s) !$s.busy -and $s.uiCreateVisible -and $s.uiInteriorStatus.Contains('최대 인원')} 'Invalid capacity'
 Check ((Root).roomCount -eq 0) 'invalid capacity stays in modal without creating a room'
 Capture 0 'create-modal.png'
 $null=Send-Player 0 'uiCreateSubmit' '비밀번호 훈련방' '2' $true
 $secretState=Wait-Player 0 {param($s) !$s.busy -and !$s.uiCreateVisible -and $s.uiRoomRows -eq 1 -and $s.uiRoomSelected -ne ''} 'Protected room creation'
 $secret=$secretState.uiRoomSelected
 Check (@($secretState.rooms|Where-Object {$_.id -eq $secret -and $_.hasPassword -and $_.maxUsers -eq 2}).Count -eq 1) 'create modal posts name capacity content and password then selects created room'
 Open-Create;$null=Send-Player 0 'uiCreateSubmit' '공개 훈련방' '4'
 $null=Wait-Player 0 {param($s) !$s.busy -and !$s.uiCreateVisible -and $s.uiRoomRows -eq 2} 'Public room creation'
 $null=Wait-Player 1 {param($s) !$s.busy -and $s.uiRoomRows -eq 2} 'Automatic room list update'
 Check $true 'second client receives new rooms without manual refresh'
 $null=Send-Player 0 'uiRoomSearch' '비밀번호';$null=Wait-Player 0 {param($s) $s.uiRoomRows -eq 1} 'Search'
 $null=Send-Player 0 'uiRoomProtection';$null=Wait-Player 0 {param($s) $s.uiRoomRows -eq 0} 'Public filter excludes protected search'
 $null=Send-Player 0 'uiRoomProtection';$null=Wait-Player 0 {param($s) $s.uiRoomRows -eq 1} 'Protected filter'
 $null=Send-Player 0 'uiRoomReset';$null=Wait-Player 0 {param($s) $s.uiRoomRows -eq 2} 'Filter reset'
 $null=Send-Player 0 'uiRoomSort';$id=$taskIds[0];$null=Wait-Player 0 {param($s) $s.completed -ge $id -and $s.uiRoomRows -eq 2} 'Vacancy sorting'
 Check $true 'search public and protected filters reset and sorting remain connected to room browser'
 Capture 0 'interior-1280.png'
 $null=Send-Player 0 'uiRoomSelect' $secret;$null=Wait-Player 0 {param($s) !$s.busy -and $s.uiRoomSelected -eq $secret -and $s.uiJoinRoomEnabled} 'Protected row selection'
 $null=Send-Player 0 'uiRoomJoin' 'pass1234';$null=Wait-Player 0 {param($s) !$s.busy -and $s.connected -and !$s.uiInteriorVisible -and !$s.uiInteriorInputEnabled} 'Host room connection'
 Check $true 'join button connects Netcode and disables hidden Canvas input for existing room screen'
 $taskHostState=Read-Player 0
 $null=Wait-Player 1 {param($s) @($s.lobbyParticipants|Where-Object {$_.userId -eq $taskHostState.userId -and $_.connectionState -eq 'connected'}).Count -eq 1} 'Live participant location'
 $null=Send-Player 0 'ready' '' '' $true;$null=Wait-Player 0 {param($s) !$s.busy -and $s.allReady} 'Ready'
 $null=Send-Player 0 'phase' '' '' $true;$null=Wait-Player 0 {param($s) !$s.busy -and $s.running} 'Running'
 $null=Send-Player 1 'uiRoomAvailable';$null=Wait-Player 1 {param($s) $s.uiRoomRows -eq 1} 'Running room filtered'
 Check $true 'available-only filter responds to room running state'
 $null=Send-Player 1 'uiParticipantRoom' $taskHostState.userId
 $null=Wait-Player 1 {param($s) !$s.busy -and $s.uiRoomSelected -eq $secret -and !$s.uiJoinRoomEnabled -and $s.uiRoomRows -eq 2} 'Participant room discovery'
 Check $true 'participant button finds actual room clears conflicting filters and blocks running admission'
 $null=Send-Player 0 'phase' '' '' $false;$null=Wait-Player 1 {param($s) !$s.busy -and $s.uiJoinRoomEnabled} 'Room back to waiting'
 $null=Send-Player 1 'uiRoomJoin' 'wrong-pass';$id=$taskIds[1]
 $null=Wait-Player 1 {param($s) $s.completed -ge $id -and !$s.busy -and $s.uiInteriorVisible -and $s.uiInteriorStatus.Contains('비밀번호')} 'Password rejection'
 Check ((Read-Player 1).pendingRoomId -eq '') 'wrong password shows server error without leaving a reservation'
 $null=Send-Player 1 'uiRoomJoinPaused' 'pass1234'
 $null=Wait-Player 1 {param($s) $s.busy -and $s.uiInteriorVisible -and $s.uiRoomProgressVisible -and $s.uiInteriorLayoutValid -and $s.canCancel} 'Room progress Canvas'
 Capture 1 'room-progress.png'
 $null=Send-Player 1 'uiRoomCancel'
 $null=Wait-Player 1 {param($s) !$s.busy -and $s.uiInteriorVisible -and !$s.connected -and $s.roomId -eq '' -and $s.pendingRoomId -eq '' -and !$s.uiRoomProgressVisible} 'Room cancellation cleanup'
 Check ((Read-Player 0).connected) 'uGUI cancellation clears accepted connection while preserving existing room member'
 $null=Send-Player 1 'uiRoomJoin' 'pass1234'
 $null=Wait-Player 1 {param($s) !$s.busy -and $s.connected -and @($s.participants).Count -eq 2} 'Second participant connection'
 Check $true 'password retry connects second client to original Netcode room'
 $null=Send-Player 0 'disconnect'
 $null=Wait-Player 0 {param($s) !$s.busy -and !$s.connected -and $s.uiInteriorVisible -and $s.uiReconnectVisible -and $s.uiInteriorLayoutValid} 'Reconnect controls'
 Capture 0 'reconnect.png'
 $null=Send-Player 0 'uiInteriorReconnect' 'pass1234'
 $null=Wait-Player 0 {param($s) !$s.busy -and $s.connected -and @($s.participants).Count -eq 2} 'uGUI reconnect'
 Check $true 'transport loss returns to interior reconnect controls and reconnect button restores room'
 foreach($n in @(0,1)){$null=Send-Player $n 'leave';$null=Wait-Player $n {param($s) !$s.busy -and !$s.connected -and $s.uiInteriorVisible} 'Leave room'}
 $null=Wait-Player 0 {param($s) $s.uiRoomRows -eq 1 -and @($s.lobbyParticipants|Where-Object {$_.connectionState -eq 'connected'}).Count -eq 0} 'Room deletion and participant return'
 Check $true 'empty room disappears live and participants return to lobby waiting state'
 $null=Send-Player 0 'uiResize' '960x720'
 $null=Wait-Player 0 {param($s) $s.uiWidth -eq 960 -and $s.uiHeight -eq 720 -and $s.uiInteriorLayoutValid} 'Compact interior layout'
 Capture 0 'interior-960.png'
 Check $true 'interior controls keep unblocked raycasts at actual 960 by 720 resolution'
 Open-Create;$null=Send-Player 0 'uiCreateClose';$null=Wait-Player 0 {param($s) !$s.uiCreateVisible -and $s.uiInteriorLayoutValid} 'Modal close'
 Check $true 'closing create modal restores lobby controls without submitting'
 $null=Send-Player 0 'uiInteriorLogout';$null=Wait-Player 0 {param($s) !$s.loggedIn -and !$s.busy -and $s.uiVisible -and !$s.uiInteriorVisible} 'Interior logout'
 $null=Wait-Player 1 {param($s) $s.uiParticipantRows -eq 1 -and $s.uiRoomRows -eq 0} 'Logout owner cleanup'
 Check $true 'interior logout returns to login and removes owned empty room and participant on other client'
 $null=Send-Player 1 'uiLobbyLeave';$null=Wait-Player 1 {param($s) !$s.busy -and $s.lobbyId -eq '' -and $s.uiVisible -and !$s.uiInteriorVisible} 'Lobby exit button'
 Check $true 'lobby exit button returns to catalog Canvas'
 $null=Send-Player 1 'uiLogout';$null=Wait-Player 1 {param($s) !$s.loggedIn -and !$s.busy} 'Final logout'
 foreach($n in @(0,1)){$log=Join-Path (Join-Path $taskRun $n.ToString()) 'player.log';if(Select-String -LiteralPath $log -Pattern 'NullReferenceException|MissingReferenceException|InvalidOperationException|ArgumentException|MULTICLIENT PROBE:'){throw ('UI runtime exception in player '+$n)}}
 Check $true 'both clients complete without UI runtime exceptions'
 Write-Output ('INTERIOR COMPLETE: '+$taskRun)
}finally{
 foreach($player in $taskPlayers){if(!$player.HasExited){Stop-Process -Id $player.Id -Force -ErrorAction SilentlyContinue}}
 Stop-Api
 foreach($key in $taskOld.Keys){[Environment]::SetEnvironmentVariable($key,$taskOld[$key],'Process')}
}
