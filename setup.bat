@echo off
setlocal EnableExtensions
REM ============================================================================
REM  CHRONIQ - install as an always-on background app
REM
REM    setup.bat                 Install on port 5080, sync every 60 minutes
REM    setup.bat 6060            Install on port 6060
REM    setup.bat 6060 30         Install on port 6060, sync every 30 minutes (5-1440)
REM    setup.bat stop            Stop the background app (it starts again at next logon)
REM    setup.bat uninstall       Stop it and remove the auto-start entry (data is kept)
REM
REM  Installs to publish\, starts hidden at every logon for this Windows user
REM  (Task Scheduler task "CHRONIQ"), and restarts it if it crashes.
REM  Re-run after pulling changes to rebuild and reinstall.
REM  Data stays next to this file: data\ and ChroniqKnowledge\
REM ============================================================================

cd /d "%~dp0"
set "PS=powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-autostart.ps1"

if /i "%~1"=="stop" (%PS% -Action Stop & goto :eof)
if /i "%~1"=="uninstall" (%PS% -Action Uninstall || goto :fail
  echo Auto-start removed. Your data in data\ and ChroniqKnowledge\ was kept.
  goto :eof)

set "PORT=%~1"
if "%PORT%"=="" set "PORT=5080"
set "INTERVAL=%~2"
if "%INTERVAL%"=="" set "INTERVAL=60"

REM ---- prerequisites --------------------------------------------------------
where dotnet >nul 2>&1 || (echo [ERROR] .NET SDK 10 not found. Install from https://dotnet.microsoft.com/download & goto :fail)
where npm >nul 2>&1 || (echo [ERROR] Node.js 22+ not found. Install from https://nodejs.org & goto :fail)

echo [1/4] Stopping any running background instance...
%PS% -Action Stop || goto :fail

echo [2/4] Building the web UI...
if not exist "frontend\node_modules" (
  pushd frontend
  call npm ci || (popd & goto :fail)
  popd
)
powershell -NoProfile -ExecutionPolicy Bypass -File "scripts\build-frontend.ps1" -SkipInstall || goto :fail

echo [3/4] Publishing the server to publish\ ...
dotnet publish "src\Chroniq.Api" -c Release -o publish -v quiet -nologo || goto :fail

echo [4/4] Registering auto-start and starting on port %PORT%...
%PS% -Action Install -Port %PORT% -IntervalMinutes %INTERVAL% || goto :fail

echo.
echo Done. CHRONIQ is running at http://127.0.0.1:%PORT%
echo It starts automatically when you sign in to Windows and fetches every %INTERVAL% minutes.
start "" "http://127.0.0.1:%PORT%"
goto :eof

:fail
echo.
echo [FAILED] See the messages above.
pause
exit /b 1
