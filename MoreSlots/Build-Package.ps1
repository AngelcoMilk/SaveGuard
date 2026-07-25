$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
    dotnet build .\MoreSlots.csproj -c Release
    if (Test-Path .\dist) { Remove-Item -Recurse -Force .\dist }
    New-Item -ItemType Directory -Force .\dist | Out-Null
    Copy-Item .\bin\Release\MoreSlots.dll .\dist\MoreSlots.dll -Force
    Copy-Item .\CHANGELOG.md .\dist\CHANGELOG.md -Force
    Write-Host "Build complete: dist\MoreSlots.dll"
} finally {
    Pop-Location
}
