param([string]$ServerProject,[string]$EvidenceRoot)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
$taskServerRoot=Join-Path $PSScriptRoot 'SmartTrainingServer'
if($ServerProject){$taskServerRoot=[IO.Path]::GetFullPath($ServerProject)}
$taskRun=Join-Path $PSScriptRoot ('catalog-evidence/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
if($EvidenceRoot){$taskRun=Join-Path ([IO.Path]::GetFullPath($EvidenceRoot)) (Get-Date -Format 'yyyyMMdd-HHmmss')}
New-Item -ItemType Directory -Path $taskRun -Force | Out-Null
$taskApi='http://127.0.0.1:51425'
$taskOld=@{}; $taskServer=$null; $taskClients=@()
$taskSettings=@{ASPNETCORE_ENVIRONMENT='Development';Server__Port='51425';Room__ServerApiBaseUrl=$taskApi;Room__AdvertisedAddress='127.0.0.1';Contents__Directory=$taskRun}
@'
[{"rootContent":"alpha","id":1,"title":"Alpha","children":[]},
 {"rootContent":"beta","id":2,"title":"Beta","children":[]}]
'@ | Set-Content -LiteralPath (Join-Path $taskRun 'catalog.json') -Encoding UTF8
function Check([bool]$ok,[string]$name){if(!$ok){throw $name};Write-Output ('CATALOG PASS: '+$name)}
function Post([string]$path,$body,$auth){Invoke-RestMethod ($taskApi+$path) -Method Post -Headers $auth -ContentType 'application/json' -Body ($body|ConvertTo-Json -Compress)}
function Summary([string]$id){Invoke-RestMethod ($taskApi+'/api/Lobby/'+$id) -Headers $taskAuth}
function Workers { @(Get-CimInstance Win32_Process -Filter ('ParentProcessId = '+$taskServer.Id) | Where-Object {$_.CommandLine -match ' -lobby '}) }
function Wait-State([string]$id,[string]$state){
 $end=[DateTime]::UtcNow.AddSeconds(65)
 do { $snapshot=Summary $id;if($snapshot.serverState -eq $state){return $snapshot};Start-Sleep -Milliseconds 100 }while([DateTime]::UtcNow -lt $end)
 throw ('Lobby state timeout: '+$id+' '+($snapshot|ConvertTo-Json -Compress))
}
function Begin-Join($login,[string]$id){
 $handler=New-Object System.Net.Http.HttpClientHandler;$handler.UseProxy=$false
 $client=New-Object System.Net.Http.HttpClient($handler);$client.Timeout=[TimeSpan]::FromSeconds(70)
 $client.DefaultRequestHeaders.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer',$login.accessToken)
 $script:taskClients+=,$client
 $content=New-Object System.Net.Http.StringContent(('{"lobbyId":"'+$id+'"}'),[Text.Encoding]::UTF8,'application/json')
 return $client.PostAsync(($taskApi+'/api/Lobby/join'),$content)
}
function Finish-Join($task){
 $response=$task.GetAwaiter().GetResult();$json=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
 if(!$response.IsSuccessStatusCode){throw $json};return ($json|ConvertFrom-Json)
}
try{
 foreach($key in $taskSettings.Keys){$taskOld[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,$taskSettings[$key],'Process')}
 $dll=Join-Path $taskServerRoot 'bin/Debug/net8.0/Smart Training Server.dll'
 $taskServer=Start-Process 'dotnet' -ArgumentList ('"'+$dll+'"') -WorkingDirectory $taskServerRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskRun 'server.log') -RedirectStandardError (Join-Path $taskRun 'errors.log')
 for($n=0;$n -lt 100;$n++){try{$null=Invoke-RestMethod ($taskApi+'/api/health');break}catch{if($taskServer.HasExited){throw 'API exited'};Start-Sleep -Milliseconds 100}}
 $loginA=Post '/api/Auth/login' @{userName='catalog-a'} @{}
 $loginB=Post '/api/Auth/login' @{userName='catalog-b'} @{}
 $loginC=Post '/api/Auth/login' @{userName='catalog-c'} @{}
 $taskAuth=@{Authorization=('Bearer '+$loginA.accessToken)}
 $authB=@{Authorization=('Bearer '+$loginB.accessToken)};$authC=@{Authorization=('Bearer '+$loginC.accessToken)}
 $list=Invoke-RestMethod ($taskApi+'/api/Lobby') -Headers $taskAuth
 Check (@($list).Count -eq 2 -and @($list|Where-Object {$_.serverState -ne 'offline'}).Count -eq 0 -and @(Workers).Count -eq 0) 'catalog roots listed without launching Unity workers'
 $rejected=$false;try{$null=Post '/api/Lobby/create' @{lobbyName='custom'} $taskAuth}catch{$rejected=$_.Exception.Response.StatusCode.value__ -eq 400}
 Check $rejected 'production API rejects player-created lobby'
 $first=Begin-Join $loginA 'alpha';$second=Begin-Join $loginB 'alpha'
 $a=Finish-Join $first;$b=Finish-Join $second
 Check ($a.success -and $b.success -and (Summary 'alpha').userCount -eq 2 -and @(Workers).Count -eq 1) 'concurrent joins share one real Unity lobby process'
 $c=Finish-Join (Begin-Join $loginC 'beta')
 Check ($c.success -and @(Workers).Count -eq 2 -and (Summary 'beta').serverState -eq 'running') 'two catalog lobbies run distinct Unity processes'
 $room=Post '/api/Room/create' @{lobbyId='alpha';roomName='catalog-room';contentId='alpha';maxUsers=2} $taskAuth
 Check ($room.success -and @(Workers).Count -eq 2) 'room can be created inside catalog lobby without starting empty room server'
 $alphaWorker=@(Workers | Where-Object {$_.CommandLine -match '-Id alpha '})[0]
 Stop-Process -Id $alphaWorker.ProcessId -Force
 $end=[DateTime]::UtcNow.AddSeconds(65)
 do{
  Start-Sleep -Milliseconds 100
  $replacement=@(Workers | Where-Object {$_.CommandLine -match '-Id alpha '})
 }while(($replacement.Count -ne 1 -or $replacement[0].ProcessId -eq $alphaWorker.ProcessId -or (Summary 'alpha').serverState -ne 'running') -and [DateTime]::UtcNow -lt $end)
 $replacement=@(Workers | Where-Object {$_.CommandLine -match '-Id alpha '})[0]
 Check ($replacement.ProcessId -ne $alphaWorker.ProcessId -and (Summary 'alpha').userCount -eq 2) 'crashed lobby worker restarts and preserves participants'
 $null=Post '/api/Lobby/leave' @{lobbyId='alpha'} $taskAuth
 Check ((Summary 'alpha').userCount -eq 1 -and @(Workers).Count -eq 2) 'worker stays alive while one participant remains'
 $null=Post '/api/Lobby/leave' @{lobbyId='alpha'} $authB
 $null=Wait-State 'alpha' 'offline'
 Check (@(Workers).Count -eq 1 -and (Summary 'alpha').userCount -eq 0) 'last departure stops only its lobby worker'
 $null=Post '/api/Lobby/leave' @{lobbyId='beta'} $authC
 $null=Wait-State 'beta' 'offline'
 $list=Invoke-RestMethod ($taskApi+'/api/Lobby') -Headers $taskAuth
 Check (@(Workers).Count -eq 0 -and @($list).Count -eq 2) 'empty catalog lobbies remain visible after all workers stop'
 $again=Finish-Join (Begin-Join $loginA 'alpha')
 Check ($again.success -and @(Workers).Count -eq 1) 'idle catalog lobby starts again on next entry'
 $null=Post '/api/Auth/logout' @{} $taskAuth
 $taskAuth=$authB
 Check (@(Workers).Count -eq 0 -and (Summary 'alpha').userCount -eq 0) 'logout stops final occupied lobby worker'
 Write-Output ('CATALOG COMPLETE: '+$taskRun)
}finally{
 foreach($client in $taskClients){$client.Dispose()}
 if($taskServer -and !$taskServer.HasExited){Stop-Process -Id $taskServer.Id -Force -ErrorAction SilentlyContinue}
 foreach($key in $taskOld.Keys){[Environment]::SetEnvironmentVariable($key,$taskOld[$key],'Process')}
}
