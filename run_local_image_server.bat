@echo off
setlocal

cd /d "%~dp0"

echo [1/2] Installing dependencies...
python -m pip install fastapi uvicorn requests
if errorlevel 1 (
    echo Failed to install dependencies.
    pause
    exit /b 1
)

echo [2/2] Starting local server...
python "tools\server.py"

endlocal
