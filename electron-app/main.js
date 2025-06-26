const { app, BrowserWindow, Tray, Menu, ipcMain, dialog } = require('electron');
const path = require('path');

let mainWindow = null;
let tray = null;
let isQuitting = false;

function createWindow() {
    mainWindow = new BrowserWindow({
        width: 1400,
        height: 900,
        minWidth: 1200,
        minHeight: 800,
        show: false,
        icon: path.join(__dirname, 'assets', 'icon.ico'),
        webPreferences: {
            nodeIntegration: true,
            contextIsolation: false
        },
        titleBarStyle: 'default',
        autoHideMenuBar: true
    });

    mainWindow.loadFile('index.html');

    mainWindow.on('close', (event) => {
        if (!isQuitting) {
            event.preventDefault();
            mainWindow.hide();
            
            if (tray && !mainWindow.wasMinimizedToTray) {
                tray.displayBalloon({
                    iconType: 'info',
                    title: 'Microsoft Endpoint Monitor',
                    content: 'Application minimized to system tray.'
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
    const iconPath = path.join(__dirname, 'assets', 'icon.ico');
    tray = new Tray(iconPath);

    const contextMenu = Menu.buildFromTemplate([
        {
            label: 'Microsoft Endpoint Monitor',
            enabled: false
        },
        { type: 'separator' },
        {
            label: 'Show Dashboard',
            click: () => showWindow()
        },
        {
            label: 'Hide Dashboard',
            click: () => hideWindow()
        },
        { type: 'separator' },
        {
            label: 'About',
            click: () => showAbout()
        },
        {
            label: 'Quit',
            click: () => quitApplication()
        }
    ]);

    tray.setContextMenu(contextMenu);
    tray.setToolTip('Microsoft Endpoint Monitor');
    
    tray.on('double-click', () => {
        toggleWindow();
    });
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

function showAbout() {
    dialog.showMessageBox(mainWindow, {
        type: 'info',
        title: 'About Microsoft Endpoint Monitor',
        message: 'Microsoft Endpoint Monitor v1.0.0',
        detail: 'Real-time monitoring for Microsoft services with latency tracking.\n\nCreated by BigChiefRick'
    });
}

function quitApplication() {
    isQuitting = true;
    app.quit();
}

app.whenReady().then(() => {
    createWindow();
    createTray();
});

app.on('second-instance', () => {
    showWindow();
});

app.on('before-quit', () => {
    isQuitting = true;
});

app.on('window-all-closed', () => {
    // Keep running in system tray
});

app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
        createWindow();
    }
});

console.log('Microsoft Endpoint Monitor starting...');
