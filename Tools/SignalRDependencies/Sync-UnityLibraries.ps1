param()
$ErrorActionPreference = 'Stop'
$taskProject = Join-Path $PSScriptRoot 'SignalRDependencies.csproj'
dotnet restore $taskProject --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'SignalR 종속성 복원에 실패했습니다.' }
dotnet build $taskProject -c Release --no-restore -v quiet
if ($LASTEXITCODE -ne 0) { throw 'SignalR 종속성 빌드에 실패했습니다.' }
$taskUnityRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskDestination = Join-Path $taskUnityRoot 'Assets/Plugins/SignalR'
New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
# Unity의 .NET Standard 2.1 런타임이 제공하는 타입을 중복 배포하지 않습니다.
$taskBuiltIn = @('SignalRDependencies.dll', 'System.Memory.dll', 'System.Buffers.dll', 'System.Numerics.Vectors.dll', 'System.Threading.Tasks.Extensions.dll')
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'bin/Release/netstandard2.1') -Filter '*.dll' | Where-Object { $_.Name -notin $taskBuiltIn } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $taskDestination $_.Name) -Force
}
Write-Output 'Microsoft SignalR 라이브러리 동기화 완료'
