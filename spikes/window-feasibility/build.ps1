param([switch]$Online)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Probe\Probe.csproj'
$variables = @('APPDATA', 'DOTNET_CLI_HOME', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE', 'DOTNET_ADD_GLOBAL_TOOLS_TO_PATH')
$previous = @{}
foreach ($name in $variables) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    # Keep private user NuGet configuration out of this standalone spike.
    $env:APPDATA = Join-Path $env:TEMP 'DevToolbar-feasibility-build-appdata'
    $env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'DevToolbar-feasibility-dotnet'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
    New-Item -ItemType Directory -Force -Path $env:APPDATA, $env:DOTNET_CLI_HOME | Out-Null
    $restore = @('restore', $project, '--locked-mode', '--configfile', (Join-Path $PSScriptRoot 'NuGet.Config'), '-p:NuGetAudit=false')
    if (!$Online) {
        $cache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
        $restore += @('--source', $cache, '--ignore-failed-sources')
    }
    & dotnet @restore
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed. Offline mode requires cached packages; -Online explicitly enables nuget.org.' }
    & dotnet build $project --no-restore -c Release -p:UseSharedCompilation=false -nodeReuse:false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed. Close the prototype processes before rebuilding.' }
    $binary = Join-Path $PSScriptRoot 'Probe\bin\Release\net10.0-windows10.0.19041.0\Probe.exe'
    $output = Split-Path -Parent $binary
    $files = Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object Name -ne 'build-manifest.json' | ForEach-Object {
        [pscustomobject]@{ path = [IO.Path]::GetRelativePath($output, $_.FullName); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    }
    [pscustomobject]@{ utc = [DateTimeOffset]::UtcNow.ToString('o'); sdk = (& dotnet --version); files = @($files) } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'build-manifest.json') -Encoding utf8
    Get-FileHash -LiteralPath $binary, (Join-Path $output 'Probe.dll') -Algorithm SHA256
}
finally {
    foreach ($name in $variables) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
