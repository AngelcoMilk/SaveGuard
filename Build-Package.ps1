$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

function Get-SingleMatchValue([string] $path, [string] $pattern, [string] $label) {
    $content = Get-Content -LiteralPath $path -Raw
    $matches = [regex]::Matches($content, $pattern)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one $label in $path, found $($matches.Count)."
    }
    return $matches[0].Groups[1].Value
}

function Assert-ExitCode([string] $operation, [int] $exitCode) {
    if ($exitCode -ne 0) {
        throw "$operation failed with exit code $exitCode."
    }
}

Push-Location $root
try {
    $pluginVersion = Get-SingleMatchValue .\Plugin.cs '(?m)^\s*public const string PluginVersion\s*=\s*"([^"]+)"' 'PluginVersion'
    $packageNamespace = Get-SingleMatchValue .\thunderstore.toml '(?m)^\s*namespace\s*=\s*"([^"]+)"' 'Thunderstore namespace'
    $packageName = Get-SingleMatchValue .\thunderstore.toml '(?m)^\s*name\s*=\s*"([^"]+)"' 'Thunderstore package name'
    $packageVersion = Get-SingleMatchValue .\thunderstore.toml '(?m)^\s*versionNumber\s*=\s*"([^"]+)"' 'Thunderstore version'
    $assemblyVersion = Get-SingleMatchValue .\Properties\AssemblyInfo.cs '(?m)^\s*\[assembly:\s*AssemblyVersion\("([^"]+)"\)\]' 'AssemblyVersion'
    $fileVersion = Get-SingleMatchValue .\Properties\AssemblyInfo.cs '(?m)^\s*\[assembly:\s*AssemblyFileVersion\("([^"]+)"\)\]' 'AssemblyFileVersion'
    $expectedAssemblyVersion = "$pluginVersion.0"

    if ($pluginVersion -ne $packageVersion -or
        $assemblyVersion -ne $expectedAssemblyVersion -or
        $fileVersion -ne $expectedAssemblyVersion) {
        throw "Version mismatch: plugin=$pluginVersion package=$packageVersion assembly=$assemblyVersion file=$fileVersion."
    }

    dotnet tool restore
    Assert-ExitCode 'dotnet tool restore' $LASTEXITCODE
    dotnet clean .\SaveGuard.csproj -c Release
    Assert-ExitCode 'dotnet clean' $LASTEXITCODE
    dotnet build .\SaveGuard.csproj -c Release
    Assert-ExitCode 'dotnet build' $LASTEXITCODE
    dotnet run --project .\SaveGuard.Tests\SaveGuard.Tests.csproj -c Release
    Assert-ExitCode 'SaveGuard tests' $LASTEXITCODE

    $builtDll = Join-Path $root 'bin\Release\SaveGuard.dll'
    $builtVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($builtDll)
    if ($builtVersion.FileVersion -ne $expectedAssemblyVersion -or
        $builtVersion.ProductVersion -ne $expectedAssemblyVersion) {
        throw "Compiled DLL version mismatch: file=$($builtVersion.FileVersion) product=$($builtVersion.ProductVersion)."
    }

    $distDir = Join-Path $root 'dist'
    $distDll = Join-Path $distDir 'SaveGuard.dll'
    if (Test-Path -LiteralPath $distDir) {
        Remove-Item -LiteralPath $distDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force $distDir | Out-Null
    Copy-Item $builtDll $distDll -Force
    Copy-Item .\CHANGELOG.md (Join-Path $distDir 'CHANGELOG.md') -Force

    $builtHash = (Get-FileHash -LiteralPath $builtDll -Algorithm SHA256).Hash.ToLowerInvariant()
    $distHash = (Get-FileHash -LiteralPath $distDll -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($builtHash -ne $distHash) {
        throw "dist/SaveGuard.dll does not match the compiled DLL."
    }

    $packagePath = Join-Path $root "build\$packageNamespace-$packageName-$packageVersion.zip"
    if (Test-Path -LiteralPath $packagePath) {
        Remove-Item -LiteralPath $packagePath -Force
    }

    dotnet tool run tcli build
    Assert-ExitCode 'tcli build' $LASTEXITCODE
    if (-not (Test-Path -LiteralPath $packagePath)) {
        throw "Expected package was not created: $packagePath"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $dllEntry = $archive.GetEntry('SaveGuard.dll')
        $manifestEntry = $archive.GetEntry('manifest.json')
        if ($null -eq $dllEntry -or $null -eq $manifestEntry) {
            throw 'Package is missing SaveGuard.dll or manifest.json.'
        }

        $dllStream = $dllEntry.Open()
        try {
            $sha = [System.Security.Cryptography.SHA256]::Create()
            try {
                $packageDllHash = [BitConverter]::ToString($sha.ComputeHash($dllStream)).Replace('-', '').ToLowerInvariant()
            } finally {
                $sha.Dispose()
            }
        } finally {
            $dllStream.Dispose()
        }

        if ($packageDllHash -ne $builtHash) {
            throw "Packaged SaveGuard.dll does not match the compiled DLL."
        }

        $manifestStream = $manifestEntry.Open()
        try {
            $reader = New-Object System.IO.StreamReader($manifestStream)
            try {
                $manifest = $reader.ReadToEnd() | ConvertFrom-Json
            } finally {
                $reader.Dispose()
            }
        } finally {
            $manifestStream.Dispose()
        }

        if ($manifest.namespace -ne $packageNamespace -or
            $manifest.name -ne $packageName -or
            $manifest.version_number -ne $packageVersion) {
            throw "Package manifest mismatch: namespace=$($manifest.namespace) name=$($manifest.name) version=$($manifest.version_number)."
        }
    } finally {
        $archive.Dispose()
    }

    Write-Host "Package ready: $packagePath"
    Write-Host "SaveGuard.dll SHA-256: $builtHash"
} finally {
    Pop-Location
}
