param([Parameter(Mandatory=$true)][string]$ServerProject,[Parameter(Mandatory=$true)][string]$ClientExecutable,[Parameter(Mandatory=$true)][string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$taskRoot=$PSScriptRoot
$taskServerRoot=Join-Path $taskRoot 'SmartTrainingServer'
$taskClientExe=Join-Path $taskRoot 'UnityClient/Builds/Client/Smart Training Client.exe'
$taskRun=Join-Path $taskRoot ('resolution-evidence/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
if($ServerProject){$taskServerRoot=[IO.Path]::GetFullPath($ServerProject)}
if($ClientExecutable){$taskClientExe=[IO.Path]::GetFullPath($ClientExecutable)}
if($EvidenceRoot){$taskRun=Join-Path ([IO.Path]::GetFullPath($EvidenceRoot)) (Get-Date -Format 'yyyyMMdd-HHmmss')}
New-Item -ItemType Directory -Path $taskRun -Force | Out-Null
$taskApi='http://127.0.0.1:51465'
$taskServer=$null; $taskPlayers=@(); $taskIds=@(0,0,0); $taskOld=@{}
$taskSettings=@{ASPNETCORE_ENVIRONMENT='Development'; Server__Port='51465'; Room__ServerApiBaseUrl=$taskApi; Room__AdvertisedAddress='127.0.0.1'; SMART_TRAINING_SERVER_PROBE='1'}
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
function Act([string]$action,[string]$value='', [string]$lobbyId='', [bool]$enabled=$false){
 $id=Send-Player 0 $action $value $lobbyId $enabled
 $null=Wait-Player 0 {param($s) $s.completed -ge $id -and !$s.busy} ('Completed '+$action)
}
function Matrix([string]$name,[string]$flag,[string]$captureAction){
 foreach($dimensions in @('800x600','960x720','1280x720','1600x900','1600x600','720x1000')){
  Act 'uiResize' $dimensions
  $parts=$dimensions.Split('x');$w=[int]$parts[0];$h=[int]$parts[1]
  $s=Wait-Player 0 {param($s) $s.uiWidth -eq $w -and $s.uiHeight -eq $h -and $s.uiCanvasSpaceValid -and $s.$flag} ($name+' '+$dimensions)
  if($name -eq 'failure' -and (!$s.uiFailureVisible -or !$s.uiFailureAtTop)){throw 'Failure notice must appear at the top of the list'}
  Check $true ($name+' '+$dimensions+' keeps canvas space and reachable controls')
  if($dimensions -eq '800x600' -or $dimensions -eq '1600x600'){
   $file=$name+'-'+$dimensions+'.png';Act $captureAction $file
   if(!(Test-Path -LiteralPath (Join-Path $folder $file))){throw ('Missing capture '+$file)}
  }
 }
}
try{
 foreach($key in $taskSettings.Keys){$taskOld[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,$taskSettings[$key],'Process')}
 Start-Api
 $folder=Join-Path $taskRun '0';New-Item -ItemType Directory -Path $folder -Force|Out-Null
 $args='-screen-fullscreen 0 -screen-width 1280 -screen-height 800 --smarttraining-probe "'+$folder+'" -logFile "'+(Join-Path $folder 'player.log')+'"'
 $taskPlayers+=Start-Process $taskClientExe -ArgumentList $args -WorkingDirectory (Split-Path $taskClientExe -Parent) -WindowStyle Hidden -PassThru
 $null=Wait-Player 0 {param($s) $s.uiVisible -and $s.uiLoginVisible} 'Startup'
 Matrix 'login' 'uiLayoutValid' 'uiCapture'
 Act 'uiLogin' 'resolution-user' $taskApi
 $null=Wait-Player 0 {param($s) $s.uiRows -eq 3 -and !$s.busy} 'Catalog rows'
 Matrix 'catalog' 'uiLayoutValid' 'uiCapture'
 # Layout fixture only: simulate an admission failure message without changing the server's admission behavior.
 Act 'uiTestFailure' '로비 서버가 응답하지 않습니다. 잠시 후 다시 입장해주세요.'
 $null=Wait-Player 0 {param($s) $s.uiFailureVisible -and $s.uiFailureAtTop} 'Error notice in scroll'
 Matrix 'failure' 'uiLayoutValid' 'uiCapture'
 Act 'uiTestFailure' ''
 Act 'uiSelect' 'sample-training';Act 'uiJoin'
 $null=Wait-Player 0 {param($s) $s.uiInteriorVisible -and $s.uiChatEnabled} 'Lobby interior'
 Matrix 'interior' 'uiInteriorLayoutValid' 'uiInteriorCapture'
 Act 'uiCreateOpen';$null=Wait-Player 0 {param($s) $s.uiCreateVisible} 'Create modal'
 Matrix 'create' 'uiInteriorLayoutValid' 'uiInteriorCapture'
 Act 'uiCreateSubmit' '해상도 확인방' '4'
 $s=Wait-Player 0 {param($s) !$s.uiCreateVisible -and $s.uiRoomSelected -ne ''} 'Room created'
 Act 'uiRoomSelect' $s.uiRoomSelected;Act 'uiRoomJoin'
 $null=Wait-Player 0 {param($s) $s.uiInRoom -and $s.uiRoomMasterControls} 'Room master'
 Matrix 'room' 'uiInRoomLayoutValid' 'roomCapture'
 Act 'roomSettings';$null=Wait-Player 0 {param($s) $s.uiSettingsVisible} 'Settings modal'
 Matrix 'settings' 'uiInRoomLayoutValid' 'roomCapture'
 Act 'roomSettingsClose';Act 'roomMap' '' '' $true
 $null=Wait-Player 0 {param($s) $s.uiMapVisible} 'Map modal'
 Matrix 'map' 'uiInRoomLayoutValid' 'roomCapture'
 Act 'roomMap' '' '' $false;Act 'roomLogout'
 $null=Wait-Player 0 {param($s) !$s.loggedIn -and $s.uiLoginVisible -and !$s.busy} 'Logout'
 $errors=Select-String -LiteralPath (Join-Path $folder 'player.log') -Pattern 'NullReferenceException|MissingReferenceException|MissingComponentException|InvalidOperationException|ArgumentException|MULTICLIENT PROBE:'
 Check (!$errors) 'resolution changes finish without UI runtime exceptions'
 Write-Output ('RESOLUTION NATIVE COMPLETE: '+$taskRun)
}finally{
 foreach($player in $taskPlayers){if(!$player.HasExited){Stop-Process -Id $player.Id -Force -ErrorAction SilentlyContinue}}
 Stop-Api
 foreach($key in $taskOld.Keys){[Environment]::SetEnvironmentVariable($key,$taskOld[$key],'Process')}
}
