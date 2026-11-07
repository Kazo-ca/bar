<#
.SYNOPSIS
    Builds the Kazo Bar release zip: the toolbar (Kazo.Bar.Windows) and Kazo Bar Updater, self-contained, in one folder.

.DESCRIPTION
    Output: artifacts/Kazo.Bar-<Version>-<Runtime>.zip, files at the root of the zip.
    The asset name must stay "Kazo.Bar-<version>-win-x64.zip": the update check looks for it in each GitHub release,
    and the release tag must be "v<Version>" (for example v0.2.0 or v0.2.0-beta.1).

.EXAMPLE
    ./build/Publish-Release.ps1 -Version 0.2.0
    gh release create v0.2.0 artifacts/Kazo.Bar-0.2.0-win-x64.zip --prerelease --title "Kazo Bar 0.2.0"
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$')]
    [string] $Version,

    [ValidateSet('win-x64', 'win-arm64')]
    [string] $Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$name = "Kazo.Bar-$Version-$Runtime"
$output = Join-Path $root "artifacts/$name"
$zip = Join-Path $root "artifacts/$name.zip"

if (Test-Path $output) { Remove-Item $output -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }

$common = @('-c', 'Release', '-r', $Runtime, '--self-contained', 'true', "-p:Version=$Version", '-o', $output)
foreach ($project in 'src/Kazo.Bar.Windows/Kazo.Bar.Windows.csproj', 'src/Kazo.Bar.Updater/Kazo.Bar.Updater.csproj') {
    Write-Host "Publishing $project ($Version, $Runtime)…"
    dotnet publish (Join-Path $root $project) @common
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $project" }
}

foreach ($exe in 'Kazo.Bar.Windows.exe', 'Kazo.Bar.Updater.exe') {
    if (-not (Test-Path (Join-Path $output $exe))) { throw "$exe is missing from $output" }
}

Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip
Write-Host "Done: $zip"
Write-Host "Next: gh release create v$Version `"$zip`" --prerelease --title `"Kazo Bar $Version`""
