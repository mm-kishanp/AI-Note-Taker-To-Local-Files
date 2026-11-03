<#
.SYNOPSIS
  Runs the published app in the background. Used by the "CHRONIQ" scheduled task (see setup.bat).
  Exits with the app's exit code so the task's restart-on-failure policy kicks in after a crash.
#>
param([int]$Port = 5080)

$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root 'publish'

$env:ASPNETCORE_ENVIRONMENT = 'Production'
# Keep data\ and ChroniqKnowledge\ next to the repo, shared with run.bat.
$env:App__BaseDirectory = $root

# The content root (wwwroot, appsettings.json) is the current directory.
Set-Location $publish
& (Join-Path $publish 'Chroniq.Api.exe') --urls "http://127.0.0.1:$Port"
exit $LASTEXITCODE
