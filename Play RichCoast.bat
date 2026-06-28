@echo off
title RichCoast
cd /d "%~dp0"

echo ============================================
echo   RichCoast - starting up...
echo ============================================
echo.

if not exist "node_modules" (
    echo First run: installing dependencies, please wait...
    call npm install
    echo.
)

REM Open the browser a few seconds after the server starts (no extra window).
start "" /b cmd /c "ping -n 5 127.0.0.1 >nul & start http://localhost:5173/"

echo Launching dev server at http://localhost:5173/
echo Close this window to stop the game.
echo.

call npm run dev -- --port 5173
