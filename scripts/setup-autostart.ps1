<#
.SYNOPSIS
  Installs, starts, or removes the background "Granola PI" scheduled task. Called by setup.bat.

  Install: registers a task that starts the app hidden at logon for the current user (it must run as
  you, because the Granola key lives in your Windows Credential Manager), restarts it after a crash,
  starts it now, then turns on the app's built-in scheduled sync at the given interval.
#>
param(
    [ValidateSet('Stop', 'Install', 'Uninstall')][string]$Action = 'Install',
    [ValidateRange(1, 65535)][int]$Port = 5080,
    [ValidateRange(5, 1440)][int]$IntervalMinutes = 60
)

$ErrorActionPreference = 'Stop'
$TaskName = 'Granola PI'
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root 'publish'
$launcher = Join-Path $PSScriptRoot 'start-background.ps1'
$startupCmd = Join-Path ([Environment]::GetFolderPath('Startup')) 'Granola PI.cmd'
$url = "http://127.0.0.1:$Port"

function Stop-App {
    if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
        Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    }
    # Any copy running from this repo: publish\ (task, Startup fallback, previous port) or bin\ (run.bat),
    # which would lock the build output and share the same database.
    Get-Process -Name 'GranolaPI.Api' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) } |
        ForEach-Object { Write-Host "  stopping running instance (PID $($_.Id))"; Stop-Process -Id $_.Id -Force }
    Start-Sleep -Milliseconds 500
}

function Remove-Autostart {
    if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
        Write-Host "  removed scheduled task '$TaskName'"
    }
    if (Test-Path $startupCmd) { Remove-Item $startupCmd -Force; Write-Host '  removed Startup folder entry' }
}

if ($Action -eq 'Stop') { Stop-App; exit 0 }
if ($Action -eq 'Uninstall') { Stop-App; Remove-Autostart; exit 0 }

# ---- install -------------------------------------------------------------------
$busy = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($busy) {
    $owner = Get-Process -Id $busy.OwningProcess -ErrorAction SilentlyContinue
    throw "Port $Port is already used by '$($owner.ProcessName)' (PID $($busy.OwningProcess)). Stop it or choose another port: setup.bat <port>"
}

Remove-Autostart
$psArgs = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$launcher`" -Port $Port"
# "conhost --headless" gives the app a console with no window. Without it, Windows 11 opens the console
# in Windows Terminal (which ignores -WindowStyle Hidden), and closing that window kills the app.
$headlessArgs = "--headless powershell.exe $psArgs"
$user = "$env:USERDOMAIN\$env:USERNAME"
try {
    $task = New-ScheduledTask `
        -Action (New-ScheduledTaskAction -Execute 'conhost.exe' -Argument $headlessArgs -WorkingDirectory $publish) `
        -Trigger (New-ScheduledTaskTrigger -AtLogOn -User $user) `
        -Principal (New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited) `
        -Settings (New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 999 `
            -RestartInterval (New-TimeSpan -Minutes 1) -StartWhenAvailable -MultipleInstances IgnoreNew `
            -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries) `
        -Description "Granola Project Intelligence Sync on $url (installed by setup.bat)"
    Register-ScheduledTask -TaskName $TaskName -InputObject $task | Out-Null
    Start-ScheduledTask -TaskName $TaskName
    Write-Host "  registered scheduled task '$TaskName' (starts at logon, restarts after a crash)"
}
catch {
    # Some managed machines block task registration without admin rights: fall back to the Startup folder.
    Write-Warning "Could not register a scheduled task ($($_.Exception.Message)). Using the Startup folder instead (no auto-restart after a crash)."
    Set-Content -Path $startupCmd -Encoding ascii -Value "@start `"`" conhost.exe $headlessArgs"
    Start-Process conhost.exe -ArgumentList $headlessArgs -WindowStyle Hidden
    Write-Host "  added Startup folder entry: $startupCmd"
}

Write-Host "  waiting for $url ..."
$health = $null
for ($i = 0; $i -lt 90 -and -not $health; $i++) {
    try { $health = Invoke-RestMethod "$url/api/health" -TimeoutSec 2 } catch { Start-Sleep 1 }
}
if (-not $health) { throw "The app did not answer on $url within 90 seconds. Check data\logs for errors." }

# Turn on the app's own scheduler (SynchronizationWorker) at the requested interval.
$settings = Invoke-RestMethod "$url/api/settings"
$sync = $settings.sync
$sync.autoSyncEnabled = $true
$sync.intervalMinutes = $IntervalMinutes
Invoke-RestMethod "$url/api/settings" -Method Put -Headers @{ 'X-GPI-Request' = '1' } -ContentType 'application/json' `
    -Body (@{ sync = $sync } | ConvertTo-Json -Depth 5) | Out-Null
Write-Host "  auto-sync enabled: every $IntervalMinutes minutes"

# Scheduled sync only runs when a Granola API key is stored (SynchronizationWorker.MaybeStartAsync).
if (-not (Invoke-RestMethod "$url/api/settings/credentials/granola").configured) {
    Write-Warning "No Granola API key is configured yet, so scheduled sync will not run. Add it in $url (Settings)."
}
