// Microsoft Endpoint Monitor - Renderer Process
console.log('Microsoft Endpoint Monitor renderer starting...');

// Global state
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
    startDataPolling();
});

function initializeUI() {
    updateConnectionStatus('Connected (Demo Mode)');
    updateDashboardStats(25, 12, 3, 48);
    
    // Add refresh button functionality
    const refreshBtn = document.getElementById('refreshBtn');
    if (refreshBtn) {
        refreshBtn.addEventListener('click', () => {
            refreshData();
        });
    }
    
    // Simulate real-time updates
    setInterval(() => {
        simulateDataUpdate();
    }, 5000);
}

function updateConnectionStatus(status) {
    connectionStatus = status;
    const statusElement = document.getElementById('connectionStatus');
    const statusIndicator = document.getElementById('statusIndicator');
    
    if (statusElement) {
        statusElement.textContent = status;
        
        if (statusIndicator) {
            statusIndicator.className = 'status-indicator';
            if (status.includes('Connected')) {
                statusIndicator.classList.add('connected');
            } else if (status.includes('Connecting')) {
                statusIndicator.classList.add('connecting');
            } else {
                statusIndicator.classList.add('disconnected');
            }
        }
    }
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

function startDataPolling() {
    // Simulate polling every 5 seconds
    setInterval(() => {
        // In real implementation, this would fetch from API
        console.log('Polling for updates...');
    }, 5000);
}

function refreshData() {
    console.log('Refreshing data...');
    updateConnectionStatus('Refreshing...');
    
    setTimeout(() => {
        updateConnectionStatus('Connected (Demo Mode)');
        simulateDataUpdate();
    }, 1000);
}

function simulateDataUpdate() {
    // Simulate changing data
    const total = Math.floor(Math.random() * 10) + 20;
    const microsoft = Math.floor(Math.random() * 5) + 8;
    const services = Math.floor(Math.random() * 2) + 3;
    const latency = Math.floor(Math.random() * 30) + 30;
    
    updateDashboardStats(total, microsoft, services, latency);
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

console.log('Microsoft Endpoint Monitor renderer loaded successfully');
