@echo off
chcp 65001 >nul
setlocal

set "ROOT=%~dp0"
set "TUTOR=%ROOT%Server\storybricks-tutor-gateway"
set "IMAGE=%ROOT%Server\storybricks-image-gen-web"

where node >nul 2>&1
if errorlevel 1 goto :no_node

if not exist "%TUTOR%\.env" goto :warn_tutor_env
goto :check_image_env

:warn_tutor_env
echo [WARN] Missing tutor-gateway .env - copy .env.example and set DEEPSEEK_API_KEY
echo.

:check_image_env
if not exist "%IMAGE%\.env" goto :warn_image_env
goto :check_modules

:warn_image_env
echo [WARN] Missing image-gen-web .env - copy .env.example and set DASHSCOPE_API_KEY
echo.

:check_modules
if exist "%TUTOR%\node_modules" goto :launch

echo [INFO] First run: npm install in tutor-gateway ...
pushd "%TUTOR%"
call npm install
if errorlevel 1 goto :npm_fail
popd
echo.

:launch
echo Starting StoryBricks backends ...
echo.
echo   Tutor gateway : http://127.0.0.1:8787/health
echo   Image gen     : http://127.0.0.1:8800/health
echo.

start "StoryBricks Tutor :8787" /D "%TUTOR%" cmd /k npm start
timeout /t 1 /nobreak >nul
start "StoryBricks ImageGen :8800" /D "%IMAGE%" cmd /k node server.mjs

echo Two windows opened. Close them to stop the servers.
echo Open Unity after /health looks OK.
echo.
pause
exit /b 0

:no_node
echo [ERROR] Node.js not found. Install Node.js 18+ first.
pause
exit /b 1

:npm_fail
popd
echo [ERROR] npm install failed
pause
exit /b 1
