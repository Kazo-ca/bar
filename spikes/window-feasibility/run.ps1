param(
    [ValidateSet('single', 'split', 'native', 'opaque', 'witness')][string]$Mode = 'single',
    [ValidateSet('composition', 'hwnd')][string]$Renderer = 'composition',
    [string]$LogDirectory = (Join-Path $env:TEMP 'DevToolbar-feasibility'),
    [double]$Left = 120,
    [double]$Top = 180,
    [switch]$NoShadow
)
$ErrorActionPreference = 'Stop'
$binary = Join-Path $PSScriptRoot 'Probe\bin\Release\net10.0-windows10.0.19041.0\Probe.exe'
if (!(Test-Path -LiteralPath $binary)) { throw 'Build first: pwsh ./spikes/window-feasibility/build.ps1' }
$LogDirectory = [IO.Path]::GetFullPath($LogDirectory)
if ($LogDirectory.Contains('"')) { throw 'LogDirectory cannot contain a quote.' }
New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null
$culture = [Globalization.CultureInfo]::InvariantCulture
$arguments = @('--mode', $Mode, '--renderer', $Renderer, '--log-dir', ('"' + $LogDirectory + '"'), '--left', $Left.ToString($culture), '--top', $Top.ToString($culture))
if ($NoShadow) { $arguments += '--no-shadow' }
# This script explicitly launches the interactive test window the operator must see.
$process = Start-Process -FilePath $binary -ArgumentList $arguments -WindowStyle Normal -PassThru
[pscustomobject]@{ PID = $process.Id; Mode = $Mode; Renderer = $Renderer; NoShadow = $NoShadow.IsPresent; LogDirectory = $LogDirectory }
