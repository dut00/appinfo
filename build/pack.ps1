#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Packs every AspNetCore.AppInfo package into ./artifacts, the local NuGet feed.

.DESCRIPTION
    Runs 'dotnet pack' on the solution, which packs only the projects under src/.
    The sample can then use the packages with: dotnet run --project samples/AspNetCore.AppInfo.Sample.Api -p:UseLocalPackages=true

.PARAMETER Configuration
    Build configuration. Default: Release.

.PARAMETER Version
    Overrides AppInfoPackageVersion from Directory.Build.props. Pass the same value to the sample
    (-p:AppInfoPackageVersion=<version>) so it restores the packages that were just built.

.EXAMPLE
    pwsh ./build/pack.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build/pack.ps1 -Configuration Debug

.EXAMPLE
    pwsh ./build/pack.ps1 -Version 0.2.0-local
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

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
Get-ChildItem -Path $artifacts -Filter 'AspNetCore.AppInfo*' -File | Remove-Item -Force

$packArguments = @('pack', (Join-Path $root 'AspNetCore.AppInfo.slnx'), '--configuration', $Configuration, '--output', $artifacts)
if ($Version) {
    $packArguments += "-p:AppInfoPackageVersion=$Version"
}

& dotnet @packArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet pack failed with exit code $LASTEXITCODE."
}

# Local packs keep the same version number, and NuGet never refreshes a version it has already cached.
# Drop the cached copies so the next restore takes the packages that were just built.
# Capture first: stopping the pipeline early (Select-Object -First) would kill dotnet and reset $LASTEXITCODE.
$localsOutput = @(dotnet nuget locals global-packages --list)
$localsExitCode = $LASTEXITCODE
$locals = $localsOutput | Select-String -Pattern '^global-packages:\s*(.+)$' | Select-Object -First 1
if ($localsExitCode -eq 0 -and $locals) {
    $globalPackages = $locals.Matches[0].Groups[1].Value.Trim()
    if (Test-Path -LiteralPath $globalPackages) {
        foreach ($cached in Get-ChildItem -LiteralPath $globalPackages -Directory -Filter 'aspnetcore.appinfo*') {
            try {
                Remove-Item -LiteralPath $cached.FullName -Recurse -Force
            }
            catch {
                Write-Warning "Could not remove the cached package '$($cached.FullName)': $($_.Exception.Message) Close tools that hold it, or delete it by hand."
            }
        }
    }
}
else {
    Write-Warning 'Could not find the NuGet global packages folder; cached AspNetCore.AppInfo packages were not cleared.'
}

Get-ChildItem -Path $artifacts -Filter 'AspNetCore.AppInfo*' -File | ForEach-Object { Write-Host "Packed $($_.Name)" }
