@echo off
echo ========================================
echo Microsoft Endpoint Monitor - Single EXE Builder
echo ========================================
echo.

echo 1. Installing Electron dependencies...
cd /d "electron-app"
call npm install

echo.
echo 2. Creating application icons...
powershell -ExecutionPolicy Bypass -File "create-icons.ps1"

echo.
echo 3. Building single executable...
call npm run build-win

echo.
echo 4. Checking build output...
if exist "dist\Microsoft Endpoint Monitor Setup 1.0.0.exe" (
    echo ? Single EXE created successfully!
    echo.
    echo ?? Location: electron-app\dist\
    echo ?? Installer: Microsoft Endpoint Monitor Setup 1.0.0.exe
    echo.
    echo This installer will:
    echo ? Install the app to Program Files
    echo ? Create desktop and Start Menu shortcuts
    echo ? Run in system tray with service controls
    echo ? Include dark/light mode with improved UI
    echo ? Provide service start/stop from tray menu
    echo.
    echo ?? Ready to distribute!
) else (
    echo ? Build failed. Check the output above for errors.
)

echo.
pause
