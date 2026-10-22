#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Packs every Dut00.AppInfo package into ./artifacts, the local NuGet feed.

.DESCRIPTION
    Runs 'dotnet pack' on the solution, which packs only the projects under src/ and validates them against the
    baseline version downloaded from nuget.org (PackageValidationBaselineVersion).
    The sample can then use the packages with: dotnet run --project samples/Dut00.AppInfo.Sample.Api -p:UseLocalPackages=true

.PARAMETER Configuration
    Build configuration. Default: Release.

.PARAMETER Version
    Overrides AppInfoPackageVersion from Directory.Build.props. Pass the same value to the sample
    (-p:AppInfoPackageVersion=<version>) so it restores the packages that were just built. Never use a version that
    is on nuget.org: the published package in the NuGet cache would silently replace the local one. The script
    refuses the package validation baseline version.

.EXAMPLE
    pwsh ./build/pack.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build/pack.ps1 -Configuration Debug

.EXAMPLE
    pwsh ./build/pack.ps1 -Version 1.0.1-local
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',

    [string] $Version
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'

# Removes the Dut00.AppInfo* packages that NuGet extracted from ./artifacts into the global packages folder. Local packs
# reuse their version number, and NuGet never refreshes a version it has already extracted. Packages that came from
# nuget.org, such as the package validation baseline, stay.
function Clear-LocalPackagesFromCache {
    # Capture first: stopping the pipeline early (Select-Object -First) would kill dotnet and reset $LASTEXITCODE.
    $localsOutput = @(dotnet nuget locals global-packages --list)
    $localsExitCode = $LASTEXITCODE
    $locals = $localsOutput | Select-String -Pattern '^global-packages:\s*(.+)$' | Select-Object -First 1
    if ($localsExitCode -ne 0 -or -not $locals) {
        Write-Warning 'Could not find the NuGet global packages folder; cached local Dut00.AppInfo packages were not cleared.'
        return
    }

    $globalPackages = $locals.Matches[0].Groups[1].Value.Trim()
    if (-not (Test-Path -LiteralPath $globalPackages)) {
        return
    }

    $localFeed = [System.IO.Path]::GetFullPath($artifacts).TrimEnd('\', '/')
    $removed = 0
    foreach ($id in Get-ChildItem -LiteralPath $globalPackages -Directory -Filter 'dut00.appinfo*') {
        foreach ($cached in Get-ChildItem -LiteralPath $id.FullName -Directory) {
            $metadataPath = Join-Path $cached.FullName '.nupkg.metadata'
            $source = $null
            try {
                $source = (Get-Content -LiteralPath $metadataPath -Raw -Encoding UTF8 | ConvertFrom-Json).source
            }
            catch {
                # Missing, empty or corrupt metadata: an incomplete extraction, removed below (NuGet downloads it again).
            }

            if ($source -and ($source.TrimEnd('\', '/') -ne $localFeed)) {
                continue
            }

            try {
                Remove-Item -LiteralPath $cached.FullName -Recurse -Force
                $removed++
            }
            catch {
                Write-Warning "Could not remove the cached package '$($cached.FullName)': $($_.Exception.Message) Close tools that hold it, or delete it by hand."
            }
        }
    }

    Write-Host "Removed $removed local Dut00.AppInfo package(s) from the NuGet cache ($globalPackages)."
}

if ($Version) {
    $props = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
    if ($props -match '<PackageValidationBaselineVersion>\s*([^<\s]+)\s*</PackageValidationBaselineVersion>' -and $Version -eq $Matches[1]) {
        throw "-Version $Version is the package validation baseline, which is on nuget.org. Use an unpublished version, for example $Version-local."
    }
}

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
Get-ChildItem -Path $artifacts -Filter 'Dut00.AppInfo*' -File | Remove-Item -Force

# Before: a local build cached under a published version must not stand in for the package validation baseline.
Clear-LocalPackagesFromCache

$packArguments = @('pack', (Join-Path $root 'Dut00.AppInfo.slnx'), '--configuration', $Configuration, '--output', $artifacts)
if ($Version) {
    $packArguments += "-p:AppInfoPackageVersion=$Version"
}

& dotnet @packArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed with exit code $LASTEXITCODE."
}

# After: the next restore of the sample must take the packages that were just built.
Clear-LocalPackagesFromCache

Get-ChildItem -Path $artifacts -Filter 'Dut00.AppInfo*' -File | ForEach-Object { Write-Host "Packed $($_.Name)" }
