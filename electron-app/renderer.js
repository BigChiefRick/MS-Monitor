// Microsoft Endpoint Monitor - Enhanced Renderer with Service Control
console.log('Microsoft Endpoint Monitor renderer starting...');

// Configuration
const API_BASE_URL = 'http://localhost:5000';
const SIGNALR_HUB_URL = 'http://localhost:5000/networkhub';

// Global state
let connection = null;
let connectionStatus = 'Disconnected';
let dashboardData = {
    totalConnections: 0,
    microsoftConnections: 0,
    activeServices: [],
    recentConnections: []
};

// Service badge classification
function getServiceBadgeClass(serviceName) {
    const service = serviceName.toLowerCase();
    if (service.includes('teams')) return 'teams';
    if (service.includes('outlook')) return 'outlook';
    if (service.includes('onedrive')) return 'onedrive';
    if (service.includes('edge')) return 'edge';
    if (service.includes('office') || service.includes('excel') || service.includes('word') || service.includes('powerpoint')) return 'office';
    if (service.includes('sharepoint')) return 'sharepoint';
    return 'default';
}

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    console.log('DOM loaded, initializing Microsoft Endpoint Monitor...');
    
    initializeUI();
    connectToSignalR();
    startDataPolling();
    initializeCharts();
    
    // Check if running in Electron
    if (typeof require !== 'undefined') {
        try {
            const { ipcRenderer } = require('electron');
            initializeElectronFeatures(ipcRenderer);
        } catch (e) {
            console.log('Not running in Electron environment');
        }
    }
});

function initializeElectronFeatures(ipcRenderer) {
    // Add service control buttons to the header
    const headerControls = document.querySelector('.header-controls');
    if (headerControls) {
        const serviceControls = document.createElement('div');
        serviceControls.className = 'service-controls';
        serviceControls.innerHTML = `
            <button id="serviceStatusBtn" class="service-btn" title="Check Service Status">
                📊 Status
            </button>
            <button id="restartServicesBtn" class="service-btn" title="Restart Services">
                🔄 Restart
            </button>
        `;
        headerControls.insertBefore(serviceControls, headerControls.firstChild);
        
        // Add CSS for service buttons
        const style = document.createElement('style');
        style.textContent = `
            .service-controls {
                display: flex;
                gap: 0.5rem;
                margin-right: 1rem;
            }
            .service-btn {
                background: var(--card-bg);
                border: 1px solid var(--border-color);
                color: var(--text-primary);
                padding: 0.375rem 0.75rem;
                border-radius: 6px;
                cursor: pointer;
                font-size: 0.8rem;
                font-weight: 500;
                transition: all 0.2s ease;
            }
            .service-btn:hover {
                background: var(--text-secondary);
                color: var(--bg-secondary);
                transform: translateY(-1px);
            }
        `;
        document.head.appendChild(style);
        
        // Add event listeners
        document.getElementById('serviceStatusBtn').addEventListener('click', async () => {
            const status = await ipcRenderer.invoke('get-service-status');
            updateConnectionStatus(`Service: ${status.service ? '✅' : '❌'} | API: ${status.api ? '✅' : '❌'}`);
        });
        
        document.getElementById('restartServicesBtn').addEventListener('click', async () => {
            updateConnectionStatus('Restarting services...');
            await ipcRenderer.invoke('control-service', 'stop', 'api');
            setTimeout(async () => {
                await ipcRenderer.invoke('control-service', 'start', 'api');
            }, 2000);
        });
    }
}

function initializeUI() {
    updateConnectionStatus('Connecting...');
    updateDashboardStats(0, 0, 0, 0);
    
    // Add refresh button functionality
    const refreshBtn = document.getElementById('refreshBtn');
    if (refreshBtn) {
        refreshBtn.addEventListener('click', () => {
            refreshData();
        });
    }
}

function updateConnectionStatus(status) {
    connectionStatus = status;
    const statusElement = document.getElementById('connectionStatus');
    const statusIndicator = document.getElementById('statusIndicator');
    
    if (statusElement) {
        statusElement.textContent = status;
        
        // Update status indicator color
        if (statusIndicator) {
            statusIndicator.className = 'status-indicator';
            if (status.includes('Connected') || status.includes('✅')) {
                statusIndicator.classList.add('connected');
            } else if (status.includes('Connecting') || status.includes('Restarting')) {
                statusIndicator.classList.add('connecting');
            } else {
                statusIndicator.classList.add('disconnected');
            }
        }
    }
    
    console.log(`Connection status: ${status}`);
}

async function connectToSignalR() {
    try {
        if (typeof signalR === 'undefined') {
            console.warn('SignalR library not loaded, falling back to polling only');
            updateConnectionStatus('Connected (Polling Mode)');
            return;
        }
        
        connection = new signalR.HubConnectionBuilder()
            .withUrl(SIGNALR_HUB_URL)
            .withAutomaticReconnect()
            .build();
        
        // Handle connection events
        connection.onreconnecting((error) => {
            console.log('SignalR reconnecting...', error);
            updateConnectionStatus('Reconnecting...');
        });
        
        connection.onreconnected((connectionId) => {
            console.log('SignalR reconnected:', connectionId);
            updateConnectionStatus('Connected');
        });
        
        connection.onclose((error) => {
            console.log('SignalR connection closed:', error);
            updateConnectionStatus('Disconnected');
        });
        
        // Handle incoming data
        connection.on('DashboardUpdate', (data) => {
            console.log('Received dashboard update:', data);
            updateDashboardData(data);
        });
        
        connection.on('ConnectionEvent', (event) => {
            console.log('Received connection event:', event);
            handleConnectionEvent(event);
        });
        
        connection.on('AlertReceived', (alert) => {
            console.log('Received alert:', alert);
            showAlert(alert);
        });
        
        // Start the connection
        await connection.start();
        console.log('SignalR connected successfully');
        updateConnectionStatus('Connected');
        
    } catch (error) {
        console.error('SignalR connection failed:', error);
        updateConnectionStatus('Connected (Polling Mode)');
    }
}

async function startDataPolling() {
    // Poll the API every 5 seconds for dashboard data
    setInterval(async () => {
        try {
            await refreshData();
        } catch (error) {
            console.error('Error polling data:', error);
        }
    }, 5000);
    
    // Initial load
    await refreshData();
}

async function refreshData() {
    try {
        // Fetch dashboard data
        const dashboardResponse = await fetch(`${API_BASE_URL}/api/network/dashboard`);
        if (dashboardResponse.ok) {
            const data = await dashboardResponse.json();
            updateDashboardData(data);
        }
        
        // Fetch connections
        const connectionsResponse = await fetch(`${API_BASE_URL}/api/network/connections/microsoft`);
        if (connectionsResponse.ok) {
            const connections = await connectionsResponse.json();
            updateConnectionsTable(connections);
        }
        
        // Fetch services
        const servicesResponse = await fetch(`${API_BASE_URL}/api/network/services`);
        if (servicesResponse.ok) {
            const services = await servicesResponse.json();
            updateServicesTable(services);
        }
        
        // Update last refresh time
        updateLastRefreshTime();
        
    } catch (error) {
        console.error('Error refreshing data:', error);
        updateConnectionStatus('Error - Check API');
    }
}

function updateDashboardData(data) {
    dashboardData = { ...dashboardData, ...data };
    
    // Update stats cards
    updateDashboardStats(
        data.totalConnections || 0,
        data.microsoftConnections || 0,
        data.activeServices?.length || 0,
        calculateAverageLatency(data.activeServices || [])
    );
    
    // Update charts if they exist
    updateLatencyChart(data.activeServices || []);
    updateConnectionsChart(data);
}

function updateDashboardStats(total, microsoft, services, avgLatency) {
    const updateElement = (id, value, suffix = '') => {
        const element = document.getElementById(id);
        if (element) {
            element.textContent = value + suffix;
        }
    };
    
    updateElement('totalConnections', total);
    updateElement('microsoftConnections', microsoft);
    updateElement('activeServices', services);
    updateElement('averageLatency', Math.round(avgLatency), 'ms');
}

function updateConnectionsTable(connections) {
    const tbody = document.getElementById('connectionsTableBody');
    if (!tbody) return;
    
    tbody.innerHTML = '';
    
    connections.slice(0, 10).forEach(conn => {
        const row = document.createElement('tr');
        
        const latencyClass = getLatencyClass(conn.latency);
        const latencyText = conn.latency ? `${Math.round(conn.latency)}ms` : 'N/A';
        const serviceBadgeClass = getServiceBadgeClass(conn.serviceName || 'Unknown');
        
        row.innerHTML = `
            <td><span class="service-badge ${serviceBadgeClass}">${conn.serviceName || 'Unknown'}</span></td>
            <td class="process-name">${conn.processName || 'Unknown'}</td>
            <td class="endpoint-address">${conn.remoteAddress}:${conn.remotePort}</td>
            <td><span class="latency ${latencyClass}">${latencyText}</span></td>
            <td><span class="status-badge status-${conn.state?.toLowerCase() || 'unknown'}">${conn.state || 'Unknown'}</span></td>
            <td class="timestamp">${formatTimestamp(conn.timestamp)}</td>
        `;
        
        tbody.appendChild(row);
    });
}

function updateServicesTable(services) {
    const tbody = document.getElementById('servicesTableBody');
    if (!tbody) return;
    
    tbody.innerHTML = '';
    
    services.forEach(service => {
        const row = document.createElement('tr');
        
        const latencyClass = getLatencyClass(service.avgLatency);
        const latencyText = service.avgLatency ? `${Math.round(service.avgLatency)}ms` : 'N/A';
        const serviceBadgeClass = getServiceBadgeClass(service.name);
        
        row.innerHTML = `
            <td><span class="service-badge ${serviceBadgeClass}">${service.name}</span></td>
            <td class="connection-count">${service.connections || 0}</td>
            <td class="process-list">${(service.processes || []).join(', ')}</td>
            <td><span class="latency ${latencyClass}">${latencyText}</span></td>
            <td><span class="status-badge status-active">Active</span></td>
        `;
        
        tbody.appendChild(row);
    });
}

function getLatencyClass(latency) {
    if (!latency) return 'unknown';
    if (latency < 30) return 'good';
    if (latency < 100) return 'fair';
    return 'poor';
}

function calculateAverageLatency(services) {
    if (!services || services.length === 0) return 0;
    
    const validLatencies = services
        .map(s => s.avgLatency)
        .filter(l => l && l > 0);
    
    if (validLatencies.length === 0) return 0;
    
    return validLatencies.reduce((sum, l) => sum + l, 0) / validLatencies.length;
}

function formatTimestamp(timestamp) {
    if (!timestamp) return 'N/A';
    
    const date = new Date(timestamp);
    return date.toLocaleTimeString();
}

function updateLastRefreshTime() {
    const element = document.getElementById('lastRefresh');
    if (element) {
        element.textContent = new Date().toLocaleTimeString();
    }
}

function showAlert(alert) {
    console.log('Alert:', alert);
    
    // Create a simple notification
    const notification = document.createElement('div');
    notification.className = `alert alert-${alert.severity?.toLowerCase() || 'info'}`;
    notification.innerHTML = `
        <strong>${alert.title}</strong>
        ${alert.description ? `<br>${alert.description}` : ''}
    `;
    
    // Add to alerts container if it exists
    const alertsContainer = document.getElementById('alertsContainer');
    if (alertsContainer) {
        alertsContainer.appendChild(notification);
        
        // Remove after 5 seconds
        setTimeout(() => {
            if (notification.parentNode) {
                notification.parentNode.removeChild(notification);
            }
        }, 5000);
    }
}

function handleConnectionEvent(event) {
    console.log('Connection event:', event);
    // Could update the UI based on connection events
}

// Chart functions
let latencyChart = null;
let connectionsChart = null;

function initializeCharts() {
    if (typeof Chart === 'undefined') {
        console.warn('Chart.js not loaded, charts will not be available');
        return;
    }
    
    initializeLatencyChart();
    initializeConnectionsChart();
}

function initializeLatencyChart() {
    const ctx = document.getElementById('latencyChart');
    if (!ctx) return;
    
    latencyChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: [],
            datasets: [{
                label: 'Average Latency (ms)',
                data: [],
                borderColor: '#3b82f6',
                backgroundColor: 'rgba(59, 130, 246, 0.1)',
                tension: 0.1,
                fill: true
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                y: {
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: 'Latency (ms)',
                        color: getComputedStyle(document.documentElement).getPropertyValue('--text-secondary')
                    },
                    grid: {
                        color: getComputedStyle(document.documentElement).getPropertyValue('--border-color')
                    },
                    ticks: {
                        color: getComputedStyle(document.documentElement).getPropertyValue('--text-secondary')
                    }
                },
                x: {
                    grid: {
                        color: getComputedStyle(document.documentElement).getPropertyValue('--border-color')
                    },
                    ticks: {
                        color: getComputedStyle(document.documentElement).getPropertyValue('--text-secondary')
                    }
                }
            },
            plugins: {
                legend: {
                    labels: {
                        color: getComputedStyle(document.documentElement).getPropertyValue('--text-primary')
                    }
                }
            },
            animation: {
                duration: 300
            }
        }
    });
}

function initializeConnectionsChart() {
    const ctx = document.getElementById('connectionsChart');
    if (!ctx) return;
    
    connectionsChart = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: ['Microsoft Connections', 'Other Connections'],
            datasets: [{
                data: [0, 0],
                backgroundColor: ['#3b82f6', '#e5e7eb'],
                borderWidth: 0
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: {
                        color: getComputedStyle(document.documentElement).getPropertyValue('--text-primary'),
                        padding: 20
                    }
                }
            }
        }
    });
}

function updateLatencyChart(services) {
    if (!latencyChart || !services) return;
    
    const now = new Date().toLocaleTimeString();
    const avgLatency = calculateAverageLatency(services);
    
    // Add new data point
    latencyChart.data.labels.push(now);
    latencyChart.data.datasets[0].data.push(avgLatency);
    
    // Keep only last 20 data points
    if (latencyChart.data.labels.length > 20) {
        latencyChart.data.labels.shift();
        latencyChart.data.datasets[0].data.shift();
    }
    
    latencyChart.update('none');
}

function updateConnectionsChart(data) {
    if (!connectionsChart) return;
    
    const microsoft = data.microsoftConnections || 0;
    const total = data.totalConnections || 0;
    const other = Math.max(0, total - microsoft);
    
    connectionsChart.data.datasets[0].data = [microsoft, other];
    connectionsChart.update('none');
}

// Theme management
function toggleTheme() {
    const currentTheme = document.documentElement.getAttribute('data-theme');
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
    
    document.documentElement.setAttribute('data-theme', newTheme);
    
    const themeIcon = document.getElementById('themeIcon');
    const themeText = document.getElementById('themeText');
    
    if (newTheme === 'dark') {
        themeIcon.textContent = '☀️';
        themeText.textContent = 'Light Mode';
    } else {
        themeIcon.textContent = '🌙';
        themeText.textContent = 'Dark Mode';
    }
    
    // Save preference
    if (typeof localStorage !== 'undefined') {
        localStorage.setItem('theme', newTheme);
    }
    
    // Update charts if they exist
    if (latencyChart) {
        latencyChart.update();
    }
    if (connectionsChart) {
        connectionsChart.update();
    }
}

// Load saved theme
document.addEventListener('DOMContentLoaded', function() {
    let savedTheme = 'light';
    
    if (typeof localStorage !== 'undefined') {
        savedTheme = localStorage.getItem('theme') || 'light';
    }
    
    document.documentElement.setAttribute('data-theme', savedTheme);
    
    if (savedTheme === 'dark') {
        const themeIcon = document.getElementById('themeIcon');
        const themeText = document.getElementById('themeText');
        if (themeIcon) themeIcon.textContent = '☀️';
        if (themeText) themeText.textContent = 'Light Mode';
    }
});

// Export functions for debugging
if (typeof window !== 'undefined') {
    window.msMonitor = {
        refreshData,
        connectionStatus: () => connectionStatus,
        dashboardData: () => dashboardData,
        reconnect: connectToSignalR,
        toggleTheme
    };
}

console.log('Microsoft Endpoint Monitor renderer loaded successfully');
