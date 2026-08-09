@echo off
echo Stopping Zach Hair Studio services...

for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5236 " ^| findstr "LISTENING"') do (
    taskkill /PID %%a /T /F >nul 2>&1
)

for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":3000 " ^| findstr "LISTENING"') do (
    taskkill /PID %%a /T /F >nul 2>&1
)

for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":3001 " ^| findstr "LISTENING"') do (
    taskkill /PID %%a /T /F >nul 2>&1
)

echo All services stopped.
pause
