// Microsoft Endpoint Monitor - Real-time Renderer
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

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    console.log('DOM loaded, initializing Microsoft Endpoint Monitor...');
    
    initializeUI();
    connectToSignalR();
    startDataPolling();
    
    // Initialize charts
    initializeCharts();
});

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
            if (status === 'Connected') {
                statusIndicator.classList.add('connected');
            } else if (status === 'Connecting...') {
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
        // Use the SignalR client library (should be included in your HTML)
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
        
        row.innerHTML = `
            <td><span class="service-badge">${conn.serviceName || 'Unknown'}</span></td>
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
        
        row.innerHTML = `
            <td><span class="service-badge">${service.name}</span></td>
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

// Chart functions (simple implementations)
let latencyChart = null;
let connectionsChart = null;

function initializeCharts() {
    // Initialize Chart.js charts if the library is available
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
                borderColor: '#4CAF50',
                backgroundColor: 'rgba(76, 175, 80, 0.1)',
                tension: 0.1
            }]
        },
        options: {
            responsive: true,
            scales: {
                y: {
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: 'Latency (ms)'
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
                backgroundColor: ['#2196F3', '#E0E0E0'],
                borderWidth: 0
            }]
        },
        options: {
            responsive: true,
            plugins: {
                legend: {
                    position: 'bottom'
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

// Export functions for debugging
window.msMonitor = {
    refreshData,
    connectionStatus: () => connectionStatus,
    dashboardData: () => dashboardData,
    reconnect: connectToSignalR
};

console.log('Microsoft Endpoint Monitor renderer loaded successfully');
