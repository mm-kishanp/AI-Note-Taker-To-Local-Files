# Builds the React UI and copies it into the API's wwwroot, where it is served with SPA fallback.
# Usage: ./scripts/build-frontend.ps1 [-SkipInstall]
param([switch]$SkipInstall)
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $repo 'frontend'
$wwwroot = Join-Path $repo 'src/Chroniq.Api/wwwroot'

Push-Location $frontend
try {
    if (-not $SkipInstall) { npm ci; if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' } }
    npm run build; if ($LASTEXITCODE -ne 0) { throw 'npm run build failed' }
}
finally { Pop-Location }

if (Test-Path $wwwroot) { Remove-Item -Recurse -Force $wwwroot }
New-Item -ItemType Directory -Force $wwwroot | Out-Null
Copy-Item -Recurse -Force (Join-Path $frontend 'dist/*') $wwwroot
Write-Host "Frontend built and copied to $wwwroot"
