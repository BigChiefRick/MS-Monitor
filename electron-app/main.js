const { app, BrowserWindow, Tray, Menu, ipcMain, dialog } = require('electron');
const path = require('path');
const { spawn, exec } = require('child_process');
const fs = require('fs');

// Prevent multiple instances
const gotTheLock = app.requestSingleInstanceLock();
if (!gotTheLock) {
    app.quit();
    return;
}

let mainWindow = null;
let tray = null;
let apiProcess = null;
let serviceProcess = null;
let isQuitting = false;

const SERVICE_NAME = 'MicrosoftEndpointMonitor';
const API_PORT = 5000;

// Paths for installed version
const INSTALL_PATH = 'C:\\Program Files\\Microsoft Endpoint Monitor';
const API_PATH = path.join(INSTALL_PATH, 'src', 'MicrosoftEndpointMonitor.Api');
const SERVICE_PATH = path.join(INSTALL_PATH, 'src', 'MicrosoftEndpointMonitor.Service');

function createWindow() {
    mainWindow = new BrowserWindow({
        width: 1400,
        height: 900,
        minWidth: 1200,
        minHeight: 800,
        show: false, // Start hidden, show from tray
        icon: path.join(__dirname, 'assets', 'icon.png'),
        webPreferences: {
            nodeIntegration: true,
            contextIsolation: false,
            enableRemoteModule: true,
            webSecurity: false // Only for development
        },
        titleBarStyle: 'default',
        autoHideMenuBar: true
    });

    mainWindow.loadFile('index.html');

    // Handle window close - minimize to tray instead
    mainWindow.on('close', (event) => {
        if (!isQuitting) {
            event.preventDefault();
            mainWindow.hide();
            
            // Show notification on first minimize
            if (tray && !mainWindow.wasMinimizedToTray) {
                tray.displayBalloon({
                    iconType: 'info',
                    title: 'Microsoft Endpoint Monitor',
                    content: 'Application minimized to system tray. Right-click the tray icon to access controls.'
                });
                mainWindow.wasMinimizedToTray = true;
            }
        }
    });

    mainWindow.on('closed', () => {
        mainWindow = null;
    });
}

function createTray() {
    // Create tray icon
    const iconPath = path.join(__dirname, 'assets', 'tray-icon.png');
    
    // Create a simple icon if it doesn't exist
    if (!fs.existsSync(iconPath)) {
        // Use Windows built-in icon or create a simple one
        const defaultIcon = path.join(__dirname, 'assets', 'icon.ico');
        if (fs.existsSync(defaultIcon)) {
            tray = new Tray(defaultIcon);
        } else {
            // Fallback to app icon
            tray = new Tray(nativeImage.createEmpty());
        }
    } else {
        tray = new Tray(iconPath);
    }

    // Create tray menu
    updateTrayMenu();

    tray.setToolTip('Microsoft Endpoint Monitor - Real-time endpoint monitoring');
    
    // Double-click to show window
    tray.on('double-click', () => {
        toggleWindow();
    });
}

function updateTrayMenu() {
    const contextMenu = Menu.buildFromTemplate([
        {
            label: 'Microsoft Endpoint Monitor',
            enabled: false,
            icon: path.join(__dirname, 'assets', 'icon-16.png')
        },
        { type: 'separator' },
        {
            label: 'Show Dashboard',
            click: () => showWindow(),
            accelerator: 'CmdOrCtrl+D'
        },
        {
            label: 'Hide Dashboard',
            click: () => hideWindow(),
            enabled: mainWindow && mainWindow.isVisible()
        },
        { type: 'separator' },
        {
            label: 'Services',
            submenu: [
                {
                    label: 'Start Monitoring Service',
                    click: () => startMonitoringService(),
                    id: 'start-service'
                },
                {
                    label: 'Stop Monitoring Service',
                    click: () => stopMonitoringService(),
                    id: 'stop-service'
                },
                { type: 'separator' },
                {
                    label: 'Start API Service',
                    click: () => startApiService(),
                    id: 'start-api'
                },
                {
                    label: 'Stop API Service',
                    click: () => stopApiService(),
                    id: 'stop-api'
                },
                { type: 'separator' },
                {
                    label: 'Check Service Status',
                    click: () => checkServiceStatus()
                }
            ]
        },
        {
            label: 'Tools',
            submenu: [
                {
                    label: 'Open API in Browser',
                    click: () => {
                        require('electron').shell.openExternal('http://localhost:5000/api/network/health');
                    }
                },
                {
                    label: 'View Logs',
                    click: () => viewLogs()
                },
                {
                    label: 'Settings',
                    click: () => showSettings()
                }
            ]
        },
        { type: 'separator' },
        {
            label: 'About',
            click: () => showAbout()
        },
        {
            label: 'Quit',
            click: () => quitApplication(),
            accelerator: 'CmdOrCtrl+Q'
        }
    ]);

    tray.setContextMenu(contextMenu);
}

function showWindow() {
    if (mainWindow) {
        if (mainWindow.isMinimized()) {
            mainWindow.restore();
        }
        mainWindow.show();
        mainWindow.focus();
    } else {
        createWindow();
    }
}

function hideWindow() {
    if (mainWindow) {
        mainWindow.hide();
    }
}

function toggleWindow() {
    if (mainWindow && mainWindow.isVisible()) {
        hideWindow();
    } else {
        showWindow();
    }
}

async function startMonitoringService() {
    try {
        const result = await execPromise('sc start "MicrosoftEndpointMonitor"');
        showNotification('Service Started', 'Monitoring service started successfully');
        updateTrayMenu();
    } catch (error) {
        showNotification('Service Error', `Failed to start monitoring service: ${error.message}`);
    }
}

async function stopMonitoringService() {
    try {
        const result = await execPromise('sc stop "MicrosoftEndpointMonitor"');
        showNotification('Service Stopped', 'Monitoring service stopped successfully');
        updateTrayMenu();
    } catch (error) {
        showNotification('Service Error', `Failed to stop monitoring service: ${error.message}`);
    }
}

function startApiService() {
    if (apiProcess) {
        showNotification('API Service', 'API service is already running');
        return;
    }

    try {
        // Start API service
        apiProcess = spawn('dotnet', ['run', '--configuration', 'Release'], {
            cwd: API_PATH,
            detached: false,
            stdio: ['ignore', 'pipe', 'pipe']
        });

        apiProcess.stdout.on('data', (data) => {
            console.log(`API: ${data}`);
            if (data.includes('Now listening on')) {
                showNotification('API Started', 'API service is now running on http://localhost:5000');
                updateTrayMenu();
            }
        });

        apiProcess.stderr.on('data', (data) => {
            console.error(`API Error: ${data}`);
        });

        apiProcess.on('close', (code) => {
            console.log(`API process exited with code ${code}`);
            apiProcess = null;
            updateTrayMenu();
        });

        setTimeout(() => {
            if (apiProcess) {
                showNotification('API Starting', 'API service is starting... This may take a moment.');
            }
        }, 2000);

    } catch (error) {
        showNotification('API Error', `Failed to start API service: ${error.message}`);
    }
}

function stopApiService() {
    if (apiProcess) {
        apiProcess.kill();
        apiProcess = null;
        showNotification('API Stopped', 'API service has been stopped');
        updateTrayMenu();
    } else {
        showNotification('API Service', 'API service is not running');
    }
}

async function checkServiceStatus() {
    try {
        const serviceStatus = await execPromise('sc query "MicrosoftEndpointMonitor"');
        const isRunning = serviceStatus.includes('RUNNING');
        
        const apiStatus = apiProcess ? 'Running' : 'Stopped';
        
        showNotification('Service Status', 
            `Monitoring Service: ${isRunning ? 'Running' : 'Stopped'}\n` +
            `API Service: ${apiStatus}`
        );
    } catch (error) {
        showNotification('Status Check', 'Could not check service status');
    }
}

function viewLogs() {
    // Open Windows Event Viewer or log file location
    exec('eventvwr.msc');
}

function showSettings() {
    dialog.showMessageBox(mainWindow, {
        type: 'info',
        title: 'Settings',
        message: 'Settings panel will be available in a future update.',
        detail: 'For now, you can configure settings by editing the appsettings.json files in the installation directory.'
    });
}

function showAbout() {
    dialog.showMessageBox(mainWindow, {
        type: 'info',
        title: 'About Microsoft Endpoint Monitor',
        message: 'Microsoft Endpoint Monitor v1.0.0',
        detail: 'Real-time monitoring for Microsoft services with latency tracking.\n\n' +
                'Created by BigChiefRick\n' +
                'Built with Electron, .NET 8.0, and modern web technologies.\n\n' +
                'This application monitors your network connections to Microsoft services ' +
                'and provides real-time latency measurements and connection analysis.'
    });
}

function showNotification(title, message) {
    if (tray) {
        tray.displayBalloon({
            iconType: 'info',
            title: title,
            content: message
        });
    }
}

function quitApplication() {
    isQuitting = true;
    
    // Stop API service
    if (apiProcess) {
        apiProcess.kill();
    }
    
    // Note: We don't stop the Windows service as it should continue running
    // unless explicitly stopped by the user
    
    app.quit();
}

function execPromise(command) {
    return new Promise((resolve, reject) => {
        exec(command, (error, stdout, stderr) => {
            if (error) {
                reject(error);
            } else {
                resolve(stdout);
            }
        });
    });
}

// App event handlers
app.whenReady().then(() => {
    createWindow();
    createTray();
    
    // Auto-start API service if not running
    setTimeout(() => {
        startApiService();
    }, 3000);
});

app.on('second-instance', () => {
    // Focus window if someone tries to run a second instance
    showWindow();
});

app.on('before-quit', () => {
    isQuitting = true;
});

app.on('window-all-closed', () => {
    // Don't quit on window close, keep running in tray
    if (process.platform !== 'darwin') {
        // Keep running in system tray
    }
});

app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
        createWindow();
    }
});

// IPC handlers for renderer process
ipcMain.handle('get-service-status', async () => {
    try {
        const serviceStatus = await execPromise('sc query "MicrosoftEndpointMonitor"');
        const isServiceRunning = serviceStatus.includes('RUNNING');
        const isApiRunning = apiProcess !== null;
        
        return {
            service: isServiceRunning,
            api: isApiRunning
        };
    } catch (error) {
        return {
            service: false,
            api: apiProcess !== null
        };
    }
});

ipcMain.handle('control-service', async (event, action, type) => {
    switch (action) {
        case 'start':
            if (type === 'api') {
                startApiService();
            } else {
                await startMonitoringService();
            }
            break;
        case 'stop':
            if (type === 'api') {
                stopApiService();
            } else {
                await stopMonitoringService();
            }
            break;
    }
});

console.log('Microsoft Endpoint Monitor starting...');
