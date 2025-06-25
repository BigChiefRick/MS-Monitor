// Microsoft Endpoint Monitor - Electron Renderer Process
// Handles UI interactions and SignalR communication

const { ipcRenderer } = require('electron');

// Application state
let signalRConnection = null;
let isConnected = false;
let currentPage = 'dashboard';
let connectionData = [];
let serviceData = [];
let alertData = [];
let dashboardChart = null;

// Configuration
const API_BASE_URL = 'http://localhost:5000';
const SIGNALR_HUB_URL = `${API_BASE_URL}/networkhub`;
const UI_REFRESH_INTERVAL = 1000;

// Initialize application when DOM is ready
document.addEventListener('DOMContentLoaded', async () => {
    console.log('Microsoft Endpoint Monitor starting...');
    
    await initializeSignalR();
    initializeUI();
    startUIRefreshTimer();
    
    // Load initial data
    await loadDashboardData();
});

// SignalR Connection Management
async function initializeSignalR() {
    try {
        updateConnectionStatus('connecting', 'Connecting to monitoring service...');
        
        signalRConnection = new signalR.HubConnectionBuilder()
            .withUrl(SIGNALR_HUB_URL)
            .withAutomaticReconnect([0, 2000, 10000, 30000])
            .build();

        // Connection event handlers
        signalRConnection.onreconnecting(() => {
            updateConnectionStatus('connecting', 'Reconnecting...');
        });

        signalRConnection.onreconnected(() => {
            updateConnectionStatus('connected', 'Connected');
            joinMonitoringGroup();
        });

        signalRConnection.onclose(() => {
            updateConnectionStatus('disconnected', 'Disconnected');
        });

        // Data event handlers
        signalRConnection.on('ConnectionUpdate', handleConnectionUpdate);
        signalRConnection.on('NewAlert', handleNewAlert);
        signalRConnection.on('DashboardUpdate', handleDashboardUpdate);
        signalRConnection.on('ServiceStatisticsUpdate', handleServiceStatisticsUpdate);

        // Start connection
        await signalRConnection.start();
        updateConnectionStatus('connected', 'Connected');
        
        // Join monitoring group
        await joinMonitoringGroup();
        
        console.log('SignalR connection established');
    } catch (error) {
        console.error('SignalR connection failed:', error);
        updateConnectionStatus('disconnected', 'Connection failed');
        
        // Retry connection every 5 seconds
        setTimeout(initializeSignalR, 5000);
    }
}

async function joinMonitoringGroup() {
    try {
        if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
            await signalRConnection.invoke('JoinMonitoring');
            console.log('Joined monitoring group');
        }
    } catch (error) {
        console.error('Failed to join monitoring group:', error);
    }
}

// SignalR Event Handlers
function handleConnectionUpdate(connectionEvent) {
    console.log('Connection update received:', connectionEvent);
    
    // Update connection data
    updateConnectionInList(connectionEvent.Connection);
    
    // Update dashboard metrics
    updateDashboardMetrics();
    
    // Add to recent connections if new
    if (connectionEvent.EventType === 'CONNECTED') {
        addToRecentConnections(connectionEvent.Connection);
    }
}

function handleNewAlert(alert) {
    console.log('New alert received:', alert);
    
    // Add to alert list
    alertData.unshift(alert);
    
    // Update alerts badge
    updateAlertsBadge();
    
    // Show notification
    showNotification('New Alert', alert.Title);
    
    // Update UI
    if (currentPage === 'alerts') {
        renderAlertsPage();
    }
}

function handleDashboardUpdate(dashboardData) {
    console.log('Dashboard update received:', dashboardData);
    updateDashboardWithData(dashboardData);
}

function handleServiceStatisticsUpdate(statistics) {
    console.log('Service statistics update received:', statistics);
    serviceData = statistics;
    
    if (currentPage === 'services') {
        renderServicesPage();
    }
}

// UI Initialization
function initializeUI() {
    // Navigation
    initializeNavigation();
    
    // Page actions
    initializePageActions();
    
    // Charts
    initializeCharts();
    
    // Menu handlers
    initializeMenuHandlers();
    
    console.log('UI initialized');
}

function initializeNavigation() {
    const navItems = document.querySelectorAll('.nav-item');
    
    navItems.forEach(item => {
        item.addEventListener('click', () => {
            const page = item.dataset.page;
            if (page) {
                navigateToPage(page);
            }
        });
    });
}

function initializePageActions() {
    // Dashboard actions
    const refreshBtn = document.getElementById('refresh-btn');
    if (refreshBtn) {
        refreshBtn.addEventListener('click', () => loadDashboardData());
    }
    
    const exportBtn = document.getElementById('export-btn');
    if (exportBtn) {
        exportBtn.addEventListener('click', () => exportData());
    }
    
    // Connection filters
    const connectionFilter = document.getElementById('connection-filter');
    if (connectionFilter) {
        connectionFilter.addEventListener('input', debounce(filterConnections, 300));
    }
    
    const serviceFilter = document.getElementById('service-filter');
    if (serviceFilter) {
        serviceFilter.addEventListener('change', filterConnections);
    }
    
    const stateFilter = document.getElementById('state-filter');
    if (stateFilter) {
        stateFilter.addEventListener('change', filterConnections);
    }
}

function initializeCharts() {
    const canvas = document.getElementById('activity-chart');
    if (canvas) {
        const ctx = canvas.getContext('2d');
        
        dashboardChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: [],
                datasets: [{
                    label: 'Connections',
                    data: [],
                    borderColor: '#0078d4',
                    backgroundColor: 'rgba(0, 120, 212, 0.1)',
                    tension: 0.4
                }, {
                    label: 'Microsoft Services',
                    data: [],
                    borderColor: '#00bcf2',
                    backgroundColor: 'rgba(0, 188, 242, 0.1)',
                    tension: 0.4
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    x: {
                        type: 'time',
                        time: {
                            unit: 'minute',
                            displayFormats: {
                                minute: 'HH:mm'
                            }
                        }
                    },
                    y: {
                        beginAtZero: true
                    }
                },
                plugins: {
                    legend: {
                        display: true,
                        position: 'top'
                    }
                }
            }
        });
    }
}

function initializeMenuHandlers() {
    // Listen for menu events from main process
    ipcRenderer.on('menu-navigate', (event, page) => {
        navigateToPage(page);
    });
    
    ipcRenderer.on('menu-refresh', () => {
        loadDashboardData();
    });
    
    ipcRenderer.on('menu-export-data', () => {
        exportData();
    });
    
    ipcRenderer.on('menu-start-monitoring', () => {
        startMonitoring();
    });
    
    ipcRenderer.on('menu-stop-monitoring', () => {
        stopMonitoring();
    });
    
    ipcRenderer.on('menu-clear-data', () => {
        clearData();
    });
    
    ipcRenderer.on('menu-open-settings', () => {
        navigateToPage('settings');
    });
}

// Navigation
function navigateToPage(page) {
    // Update active nav item
    document.querySelectorAll('.nav-item').forEach(item => {
        item.classList.remove('active');
        if (item.dataset.page === page) {
            item.classList.add('active');
        }
    });
    
    // Hide all pages
    document.querySelectorAll('.page').forEach(p => {
        p.classList.remove('active');
    });
    
    // Show target page
    const targetPage = document.getElementById(`${page}-page`);
    if (targetPage) {
        targetPage.classList.add('active');
        currentPage = page;
        
        // Load page-specific data
        loadPageData(page);
    }
}

async function loadPageData(page) {
    try {
        showLoading();
        
        switch (page) {
            case 'dashboard':
                await loadDashboardData();
                break;
            case 'connections':
                await loadConnectionsData();
                break;
            case 'services':
                await loadServicesData();
                break;
            case 'alerts':
                await loadAlertsData();
                break;
            case 'processes':
                await loadProcessesData();
                break;
        }
    } catch (error) {
        console.error(`Failed to load ${page} data:`, error);
        showError(`Failed to load ${page} data`);
    } finally {
        hideLoading();
    }
}

// Data Loading Functions
async function loadDashboardData() {
    try {
        const response = await fetch(`${API_BASE_URL}/api/network/dashboard`);
        const result = await response.json();
        
        if (result.success && result.data) {
            updateDashboardWithData(result.data);
        }
    } catch (error) {
        console.error('Failed to load dashboard data:', error);
    }
}

async function loadConnectionsData() {
    try {
        const response = await fetch(`${API_BASE_URL}/api/network/connections`);
        const result = await response.json();
        
        if (result.success && result.data) {
            connectionData = result.data.items || [];
            renderConnectionsPage();
        }
    } catch (error) {
        console.error('Failed to load connections data:', error);
    }
}

async function loadServicesData() {
    try {
        const response = await fetch(`${API_BASE_URL}/api/network/services`);
        const result = await response.json();
        
        if (result.success && result.data) {
            serviceData = result.data;
            renderServicesPage();
        }
    } catch (error) {
        console.error('Failed to load services data:', error);
    }
}

async function loadAlertsData() {
    try {
        const response = await fetch(`${API_BASE_URL}/api/network/alerts`);
        const result = await response.json();
        
        if (result.success && result.data) {
            alertData = result.data.items || [];
            renderAlertsPage();
        }
    } catch (error) {
        console.error('Failed to load alerts data:', error);
    }
}

async function loadProcessesData() {
    try {
        const response = await fetch(`${API_BASE_URL}/api/network/processes`);
        const result = await response.json();
        
        if (result.success && result.data) {
            renderProcessesPage(result.data);
        }
    } catch (error) {
        console.error('Failed to load processes data:', error);
    }
}

// UI Update Functions
function updateConnectionStatus(status, message) {
    const statusIndicator = document.getElementById('status-indicator');
    const statusText = document.getElementById('status-text');
    
    if (statusIndicator) {
        statusIndicator.className = `status-indicator ${status}`;
    }
    
    if (statusText) {
        statusText.textContent = message;
    }
    
    isConnected = status === 'connected';
}

function updateDashboardWithData(data) {
    // Update metric cards
    updateElement('metric-total-connections', data.activeConnections);
    updateElement('metric-microsoft-connections', data.microsoftConnections);
    updateElement('metric-bandwidth', formatBytes(data.currentBandwidthBytesPerSecond) + '/s');
    updateElement('metric-alerts', data.recentAlerts?.length || 0);
    
    // Update header stats
    updateElement('active-connections', data.activeConnections);
    updateElement('microsoft-connections', data.microsoftConnections);
    
    // Update service list
    if (data.topServices) {
        updateServiceList(data.topServices);
    }
    
    // Update recent connections
    if (data.recentConnections) {
        updateRecentConnections(data.recentConnections);
    }
    
    // Update recent alerts
    if (data.recentAlerts) {
        updateRecentAlerts(data.recentAlerts);
    }
}

function updateServiceList(services) {
    const serviceList = document.getElementById('service-list');
    if (!serviceList) return;
    
    serviceList.innerHTML = services.map(service => `
        <div class="service-item">
            <div class="service-info">
                <span class="service-name">${service.serviceName}</span>
                <span class="service-category">${service.serviceCategory || 'Unknown'}</span>
            </div>
            <div class="service-stats">
                <span class="connection-count">${service.connectionCount} connections</span>
                <span class="data-usage">${formatBytes(service.totalBytes)}</span>
            </div>
        </div>
    `).join('');
}

function updateRecentConnections(connections) {
    const tableBody = document.querySelector('#recent-connections-table tbody');
    if (!tableBody) return;
    
    tableBody.innerHTML = connections.slice(0, 10).map(conn => `
        <tr>
            <td>${formatTime(conn.establishedTime)}</td>
            <td>${conn.processName}</td>
            <td>${conn.microsoftService || 'Unknown'}</td>
            <td>${conn.remoteHost || conn.remoteIp}</td>
            <td><span class="status-badge status-${conn.connectionState.toLowerCase()}">${conn.connectionState}</span></td>
        </tr>
    `).join('');
}

function updateRecentAlerts(alerts) {
    const alertList = document.getElementById('alert-list');
    if (!alertList) return;
    
    alertList.innerHTML = alerts.slice(0, 5).map(alert => `
        <div class="alert-item alert-${alert.severity.toLowerCase()}">
            <div class="alert-content">
                <div class="alert-title">${alert.title}</div>
                <div class="alert-message">${alert.message}</div>
                <div class="alert-time">${formatTime(alert.createdAt)}</div>
            </div>
        </div>
    `).join('');
}

// Utility Functions
function updateElement(id, value) {
    const element = document.getElementById(id);
    if (element) {
        element.textContent = value;
    }
}

function formatBytes(bytes) {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

function formatTime(dateString) {
    const date = new Date(dateString);
    return date.toLocaleTimeString();
}

function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

function showLoading() {
    const overlay = document.getElementById('loading-overlay');
    if (overlay) {
        overlay.classList.add('visible');
    }
}

function hideLoading() {
    const overlay = document.getElementById('loading-overlay');
    if (overlay) {
        overlay.classList.remove('visible');
    }
}

function showNotification(title, message) {
    console.log(`Notification: ${title} - ${message}`);
    // Could implement toast notifications here
}

function showError(message) {
    console.error('Error:', message);
    // Could implement error display here
}

// Menu Action Handlers
async function exportData() {
    try {
        const response = await fetch(`${API_BASE_URL}/api/network/connections/export`);
        const blob = await response.blob();
        
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `connections_${new Date().toISOString().slice(0, 10)}.csv`;
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
        document.body.removeChild(a);
        
        showNotification('Export Complete', 'Connection data exported successfully');
    } catch (error) {
        console.error('Export failed:', error);
        showError('Failed to export data');
    }
}

function startMonitoring() {
    // This would send a command to start monitoring
    console.log('Start monitoring requested');
}

function stopMonitoring() {
    // This would send a command to stop monitoring
    console.log('Stop monitoring requested');
}

function clearData() {
    // This would clear all monitoring data
    console.log('Clear data requested');
}

// UI Refresh Timer
function startUIRefreshTimer() {
    setInterval(() => {
        if (isConnected && currentPage === 'dashboard') {
            // Update dashboard metrics periodically
            updateDashboardMetrics();
        }
    }, UI_REFRESH_INTERVAL);
}

function updateDashboardMetrics() {
    // Update current time-based metrics
    if (dashboardChart && connectionData.length > 0) {
        const now = new Date();
        const activeCount = connectionData.filter(c => c.isActive).length;
        const microsoftCount = connectionData.filter(c => c.isActive && c.microsoftService).length;
        
        // Add data point to chart
        dashboardChart.data.labels.push(now);
        dashboardChart.data.datasets[0].data.push(activeCount);
        dashboardChart.data.datasets[1].data.push(microsoftCount);
        
        // Keep only last 20 data points
        if (dashboardChart.data.labels.length > 20) {
            dashboardChart.data.labels.shift();
            dashboardChart.data.datasets[0].data.shift();
            dashboardChart.data.datasets[1].data.shift();
        }
        
        dashboardChart.update('none');
    }
}

// Stub functions for incomplete features
function renderConnectionsPage() {
    console.log('Rendering connections page...');
}

function renderServicesPage() {
    console.log('Rendering services page...');
}

function renderAlertsPage() {
    console.log('Rendering alerts page...');
}

function renderProcessesPage(data) {
    console.log('Rendering processes page...', data);
}

function filterConnections() {
    console.log('Filtering connections...');
}

function updateConnectionInList(connection) {
    console.log('Updating connection in list:', connection);
}

function addToRecentConnections(connection) {
    console.log('Adding to recent connections:', connection);
}

function updateAlertsBadge() {
    const badge = document.getElementById('alerts-badge');
    if (badge) {
        const unacknowledgedCount = alertData.filter(a => !a.isAcknowledged).length;
        if (unacknowledgedCount > 0) {
            badge.textContent = unacknowledgedCount;
            badge.style.display = 'block';
        } else {
            badge.style.display = 'none';
        }
    }
}

console.log('Microsoft Endpoint Monitor renderer script loaded');
