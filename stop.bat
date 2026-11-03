@echo off
setlocal EnableExtensions
REM ============================================================================
REM  CHRONIQ - stop everything started by run.bat
REM
REM    stop.bat    Stops the app (port 5080), the Vite dev server (port 5173)
REM                and closes the "CHRONIQ" windows opened by "run.bat dev".
REM ============================================================================

echo Stopping CHRONIQ...

powershell -NoProfile -Command ^
  "$found = $false;" ^
  "foreach ($port in 5080, 5173) {" ^
  "  Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |" ^
  "    Select-Object -ExpandProperty OwningProcess -Unique |" ^
  "    ForEach-Object {" ^
  "      $p = Get-Process -Id $_ -ErrorAction SilentlyContinue;" ^
  "      if ($p) { Write-Host ('  stopped {0} (PID {1}) on port {2}' -f $p.ProcessName, $p.Id, $port); Stop-Process -Id $p.Id -Force; $found = $true }" ^
  "    }" ^
  "};" ^
  "if (-not $found) { Write-Host '  nothing was running on ports 5080 or 5173' }"

REM Close the console windows opened by "run.bat dev" (no error if none are open).
taskkill /F /T /FI "WINDOWTITLE eq CHRONIQ*" >nul 2>&1

echo Done.
endlocal
