# Builds self-contained win-x64 publish + Inno Setup installer (Windows).
param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $Root "artifacts\publish\win-x64"
$InstallerDir = Join-Path $Root "artifacts\installer"

Write-Host "==> Publishing Putevie $Version (win-x64 self-contained)"
if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
New-Item -ItemType Directory -Force -Path $PublishDir, $InstallerDir | Out-Null

dotnet publish (Join-Path $Root "Putevie\Putevie.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishReadyToRun=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=$Version `
    -o $PublishDir

if (-not (Test-Path (Join-Path $PublishDir "Putevie.exe"))) {
    throw "Putevie.exe not found in publish output"
}

$portableZip = Join-Path $InstallerDir "Putevie-$Version-win-x64-portable.zip"
if (Test-Path $portableZip) { Remove-Item -Force $portableZip }
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $portableZip
Write-Host "==> Portable zip: $portableZip"

$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 7\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    Write-Host "==> Compiling Inno Setup installer via $iscc"
    & $iscc `
        (Join-Path $Root "installer\Putevie.iss") `
        "/DPublishDir=$PublishDir" `
        "/DOutputDir=$InstallerDir"
} else {
    Write-Warning "ISCC.exe not found. Install Inno Setup to build Putevie-Setup-$Version.exe"
}

Get-ChildItem $InstallerDir | Format-Table Name, Length
