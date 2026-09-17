<#
.SYNOPSIS
    Builds, tests and publishes Voice Changer Pro by Cyberkyd.

.DESCRIPTION
    Usage:
        .\build.ps1                 # restore + build (Debug) + run unit tests
        .\build.ps1 -Publish        # additionally publish a self-contained single-file x64 build to .\dist
        .\build.ps1 -Integration    # also run the real-device integration tests (opens mic + speakers)

    Requires the .NET 8 SDK. If it is not on PATH, set $env:DOTNET_ROOT to its folder
    (e.g. "$env:LOCALAPPDATA\Microsoft\dotnet") before running.
#>
[CmdletBinding()]
param(
    [switch]$Publish,
    [switch]$Installer,
    [switch]$Integration,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

if ($env:DOTNET_ROOT -and (Test-Path "$env:DOTNET_ROOT\dotnet.exe")) {
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
}
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { throw "dotnet SDK not found. Install .NET 8 SDK or set DOTNET_ROOT." }
$sdk = (& dotnet --version)
Write-Host "Using .NET SDK $sdk" -ForegroundColor Cyan

Write-Host "==> Restore" -ForegroundColor Cyan
& dotnet restore VoiceChanger.sln
if ($LASTEXITCODE -ne 0) { throw "restore failed" }

Write-Host "==> Build ($Configuration)" -ForegroundColor Cyan
& dotnet build VoiceChanger.sln -c $Configuration --no-restore -nologo
if ($LASTEXITCODE -ne 0) { throw "build failed" }

Write-Host "==> Unit tests" -ForegroundColor Cyan
if ($Integration) { $env:VCP_INTEGRATION = "1" } else { Remove-Item Env:VCP_INTEGRATION -ErrorAction SilentlyContinue }
& dotnet test tests\VoiceChanger.Core.Tests\VoiceChanger.Core.Tests.csproj -c $Configuration --no-build -nologo
if ($LASTEXITCODE -ne 0) { throw "tests failed" }

if ($Publish) {
    $dist = Join-Path $root "dist"
    Write-Host "==> Publish self-contained win-x64 to $dist" -ForegroundColor Cyan
    if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
    & dotnet publish src\VoiceChanger.App\VoiceChanger.App.csproj -c $Configuration -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true `
        -p:DebugType=none -o $dist -nologo
    if ($LASTEXITCODE -ne 0) { throw "publish failed" }
    Copy-Item (Join-Path $root "README.md") $dist
    $exe = Join-Path $dist "VoiceChangerPro.exe"
    $size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "Published $exe ($size MB)" -ForegroundColor Green
}

if ($Installer) {
    if (-not (Test-Path (Join-Path $root "dist\VoiceChangerPro.exe"))) { throw "Run with -Publish first (dist\VoiceChangerPro.exe missing)" }
    $iscc = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw "Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup" }
    Write-Host "==> Installer (Inno Setup)" -ForegroundColor Cyan
    & $iscc /Q (Join-Path $root "installer\VoiceChangerPro.iss")
    if ($LASTEXITCODE -ne 0) { throw "installer build failed" }
    $setup = Get-ChildItem (Join-Path $root "installer\Output") -Filter "VoiceChangerPro-Setup-*.exe" | Sort-Object LastWriteTime | Select-Object -Last 1
    $size = [math]::Round($setup.Length / 1MB, 1)
    Write-Host "Installer: $($setup.FullName) ($size MB)" -ForegroundColor Green
}
Write-Host "Done." -ForegroundColor Green
