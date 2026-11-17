param(
    [string]$Output = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$appProject = Join-Path $root 'src\AjazzBattery\AjazzBattery.csproj'
$setupProject = Join-Path $root 'src\AjazzBattery.Setup\AjazzBattery.Setup.csproj'
$payloadDir = Join-Path $root 'src\AjazzBattery.Setup\payload'

Write-Host '=== 1/4  Сборка приложения ===' -ForegroundColor Cyan
Get-Process AjazzBattery -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process 'AJAZZ-Battery-Setup' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

New-Item -ItemType Directory -Force $Output, $payloadDir | Out-Null

dotnet publish $appProject -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -o $Output

$appExe = Join-Path $Output 'AjazzBattery.exe'
if (-not (Test-Path $appExe)) { throw "Не найден $appExe" }

Write-Host '=== 2/4  Портативная версия ===' -ForegroundColor Cyan
$portableDir = Join-Path $env:TEMP 'ajazz-portable'
Remove-Item -Recurse -Force $portableDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $portableDir | Out-Null
Copy-Item $appExe $portableDir
Copy-Item (Join-Path $root 'README.md') $portableDir
$zip = Join-Path $Output 'AJAZZ-Battery-Portable.zip'
Remove-Item -Force $zip -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $portableDir '*') -DestinationPath $zip -CompressionLevel Optimal

Write-Host '=== 3/4  Сборка установщика ===' -ForegroundColor Cyan
Copy-Item $appExe (Join-Path $payloadDir 'AjazzBattery.exe') -Force

$setupOut = Join-Path $env:TEMP 'ajazz-setup-out'
Remove-Item -Recurse -Force $setupOut -ErrorAction SilentlyContinue
dotnet publish $setupProject -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -o $setupOut

Copy-Item (Join-Path $setupOut 'AJAZZ-Battery-Setup.exe') (Join-Path $Output 'AJAZZ-Battery-Setup.exe') -Force

Write-Host '=== 4/4  Готово ===' -ForegroundColor Cyan
Get-ChildItem $Output -Filter *.exe | ForEach-Object {
    '{0,-30} {1,6:N1} МБ' -f $_.Name, ($_.Length / 1MB)
}
Get-ChildItem $Output -Filter *.zip | ForEach-Object {
    '{0,-30} {1,6:N1} МБ' -f $_.Name, ($_.Length / 1MB)
}
