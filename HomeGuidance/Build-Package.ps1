$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root

try {
    Write-Host "=== HomeGuidance Build ==="

    # Build main project
    Write-Host "Building HomeGuidance..."
    dotnet build .\HomeGuidance.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }

    # Build and run tests
    # Tests project references the main one
    Write-Host "Running tests..."
    dotnet run --project ..\HomeGuidance.Tests\HomeGuidance.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }

    # Create dist
    if (Test-Path .\dist) { Remove-Item -Recurse -Force .\dist }
    New-Item -ItemType Directory -Force .\dist | Out-Null

    Copy-Item .\bin\Release\HomeGuidance.dll .\dist\HomeGuidance.dll -Force
    Copy-Item .\README.md .\dist\README.md -Force
    Copy-Item .\CHANGELOG.md .\dist\CHANGELOG.md -Force
    Copy-Item .\manifest.json .\dist\manifest.json -Force
    Copy-Item .\icon.png .\dist\icon.png -Force -ErrorAction SilentlyContinue

    # Optional TCLI packaging
    $tcli = Get-Command "dotnet" -ErrorAction SilentlyContinue
    if ($tcli) {
        Write-Host "Packaging with TCLI..."
        dotnet tool run tcli build --config-path .\thunderstore.toml
    }
    else {
        Write-Host "TCLI not found; dist folder ready for manual packaging."
    }

    Write-Host "Build complete: dist\HomeGuidance.dll"
}
finally {
    Pop-Location
}
