@echo off
echo Starting Zach Hair Studio stack...

:: .NET API (http://localhost:5236)
start "ZH-API" cmd /c "cd /d %~dp0API\ZachHairStudio.Api && dotnet run"

:: Give the API a head start
timeout /t 3 /nobreak >nul

:: Landing Page (http://localhost:3000)
start "ZH-Landing" cmd /c "cd /d %~dp0landing-page && npm run dev"

:: Dashboard (http://localhost:3001)
start "ZH-Dashboard" cmd /c "cd /d %~dp0dashboard && npm run dev -- -p 3001"

echo.
echo   API:        http://localhost:5236
echo   Landing:    http://localhost:3000
echo   Dashboard:  http://localhost:3001
echo.
pause
