$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
    dotnet tool restore
    dotnet build .\SaveGuard.csproj -c Release
    dotnet run --project .\SaveGuard.Tests\SaveGuard.Tests.csproj -c Release
    New-Item -ItemType Directory -Force .\dist | Out-Null
    Copy-Item .\bin\Release\SaveGuard.dll .\dist\SaveGuard.dll -Force
    Copy-Item .\CHANGELOG.md .\dist\CHANGELOG.md -Force
    dotnet tool run tcli build
} finally {
    Pop-Location
}
