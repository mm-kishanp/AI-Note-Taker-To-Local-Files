@echo off
setlocal EnableExtensions
REM ============================================================================
REM  Granola Project Intelligence Sync - one-click launcher
REM
REM    run.bat            Build the UI if needed, start the app, open the browser
REM    run.bat rebuild    Force a fresh UI build (after pulling changes)
REM    run.bat dev        Developer mode: API + Vite hot-reload UI in two windows
REM    run.bat test       Run all backend and frontend tests
REM
REM  Data lives next to this file:  data\  (database, logs)
REM                                 GranolaProjectKnowledge\  (Markdown export)
REM ============================================================================

cd /d "%~dp0"
set "ROOT=%~dp0"
set "URL=http://127.0.0.1:5080"
set "MODE=%~1"

REM ---- prerequisites --------------------------------------------------------
where dotnet >nul 2>&1 || (echo [ERROR] .NET SDK 10 not found. Install from https://dotnet.microsoft.com/download & goto :fail)
where npm >nul 2>&1 || (echo [ERROR] Node.js 22+ not found. Install from https://nodejs.org & goto :fail)

REM ---- stop an instance that is already running on the port -----------------
powershell -NoProfile -Command "Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force }" >nul 2>&1

if /i "%MODE%"=="test" goto :test
if /i "%MODE%"=="dev" goto :dev

REM ---- build the UI when missing (or on "rebuild") ---------------------------
if /i "%MODE%"=="rebuild" goto :buildui
if not exist "src\GranolaPI.Api\wwwroot\index.html" goto :buildui
goto :start

:buildui
echo [1/3] Building the web UI...
if not exist "frontend\node_modules" (
  pushd frontend
  call npm ci || (popd & goto :fail)
  popd
)
powershell -NoProfile -ExecutionPolicy Bypass -File "scripts\build-frontend.ps1" -SkipInstall || goto :fail

:start
echo [2/3] Building the server...
dotnet build "src\GranolaPI.Api" -c Release -v quiet -nologo || goto :fail

echo [3/3] Starting Granola Project Intelligence Sync at %URL%
echo       Close this window or press Ctrl+C to stop.
REM Open the browser as soon as the server answers.
start "" /b powershell -NoProfile -Command "for($i=0;$i -lt 120;$i++){try{Invoke-WebRequest '%URL%/api/health' -UseBasicParsing -TimeoutSec 2 | Out-Null; Start-Process '%URL%'; break}catch{Start-Sleep 1}}"

set "ASPNETCORE_ENVIRONMENT=Production"
set "App__BaseDirectory=%ROOT%"
dotnet run --project "src\GranolaPI.Api" -c Release --no-build --no-launch-profile --urls %URL%
goto :eof

:dev
echo Starting API (Development, Swagger at %URL%/swagger) and Vite dev server (http://localhost:5173)...
if not exist "frontend\node_modules" (
  pushd frontend
  call npm ci || (popd & goto :fail)
  popd
)
start "Granola PI - API" cmd /k "set ASPNETCORE_ENVIRONMENT=Development && dotnet run --project src\GranolaPI.Api --no-launch-profile --urls %URL%"
start "Granola PI - UI" cmd /k "cd frontend && npm run dev"
start "" /b powershell -NoProfile -Command "for($i=0;$i -lt 180;$i++){try{Invoke-WebRequest 'http://localhost:5173' -UseBasicParsing -TimeoutSec 2 | Out-Null; Start-Process 'http://localhost:5173'; break}catch{Start-Sleep 1}}"
goto :eof

:test
echo Running backend tests...
dotnet test || goto :fail
echo Running frontend tests...
pushd frontend
if not exist "node_modules" call npm ci
call npm test || (popd & goto :fail)
popd
echo.
echo All tests passed.
goto :eof

:fail
echo.
echo [FAILED] See the messages above.
pause
exit /b 1
