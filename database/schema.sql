-- Microsoft Endpoint Monitor Database Schema
-- SQLite Database Schema for Network Monitoring

-- Table: connections
-- Stores individual network connections to Microsoft endpoints
CREATE TABLE IF NOT EXISTS connections (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    pid INTEGER NOT NULL,
    process_name TEXT NOT NULL,
    process_path TEXT,
    process_command_line TEXT,
    local_ip TEXT NOT NULL,
    local_port INTEGER NOT NULL,
    remote_ip TEXT NOT NULL,
    remote_port INTEGER NOT NULL,
    remote_host TEXT,
    microsoft_service TEXT,  -- 'Teams', 'Office365', 'Azure', 'OneDrive', etc.
    service_category TEXT,   -- 'Communication', 'Storage', 'Authentication', etc.
    connection_state TEXT NOT NULL, -- 'ESTABLISHED', 'LISTENING', 'CLOSED', etc.
    protocol TEXT NOT NULL DEFAULT 'TCP', -- 'TCP', 'UDP'
    bytes_sent INTEGER DEFAULT 0,
    bytes_received INTEGER DEFAULT 0,
    packets_sent INTEGER DEFAULT 0,
    packets_received INTEGER DEFAULT 0,
    established_time DATETIME DEFAULT CURRENT_TIMESTAMP,
    last_activity_time DATETIME DEFAULT CURRENT_TIMESTAMP,
    closed_time DATETIME,
    duration_ms INTEGER,
    is_active BOOLEAN DEFAULT 1,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Table: microsoft_endpoints
-- Reference table for known Microsoft IP ranges and domains
CREATE TABLE IF NOT EXISTS microsoft_endpoints (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    ip_range TEXT,          -- CIDR notation: '13.107.6.152/31'
    domain_pattern TEXT,    -- Domain pattern: '*.microsoft.com'
    service_name TEXT NOT NULL,      -- 'Teams', 'Office365', 'Azure'
    service_category TEXT,  -- 'Communication', 'Storage', 'Cloud'
    description TEXT,
    is_active BOOLEAN DEFAULT 1,
    priority INTEGER DEFAULT 0, -- Higher priority = more specific match
    last_updated DATETIME DEFAULT CURRENT_TIMESTAMP,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Table: connection_metrics
-- Time-series data for connection performance metrics
CREATE TABLE IF NOT EXISTS connection_metrics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    connection_id INTEGER,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    bytes_per_second_in INTEGER DEFAULT 0,
    bytes_per_second_out INTEGER DEFAULT 0,
    packets_per_second_in INTEGER DEFAULT 0,
    packets_per_second_out INTEGER DEFAULT 0,
    latency_ms INTEGER,
    packet_loss_rate REAL DEFAULT 0.0,
    jitter_ms INTEGER,
    connection_quality TEXT, -- 'EXCELLENT', 'GOOD', 'FAIR', 'POOR'
    FOREIGN KEY (connection_id) REFERENCES connections(id) ON DELETE CASCADE
);

-- Table: processes
-- Cache process information to avoid repeated lookups
CREATE TABLE IF NOT EXISTS processes (
    pid INTEGER PRIMARY KEY,
    name TEXT NOT NULL,
    executable_path TEXT,
    command_line TEXT,
    start_time DATETIME,
    user_name TEXT,
    is_microsoft_app BOOLEAN DEFAULT 0,
    app_version TEXT,
    app_description TEXT,
    last_seen DATETIME DEFAULT CURRENT_TIMESTAMP,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Table: monitoring_sessions
-- Track monitoring sessions and system events
CREATE TABLE IF NOT EXISTS monitoring_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_start DATETIME DEFAULT CURRENT_TIMESTAMP,
    session_end DATETIME,
    computer_name TEXT,
    windows_version TEXT,
    service_version TEXT,
    total_connections_tracked INTEGER DEFAULT 0,
    total_microsoft_connections INTEGER DEFAULT 0,
    total_bytes_tracked INTEGER DEFAULT 0,
    session_notes TEXT
);

-- Table: network_interfaces
-- Track network interface information
CREATE TABLE IF NOT EXISTS network_interfaces (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    interface_name TEXT NOT NULL,
    interface_description TEXT,
    mac_address TEXT,
    ip_address TEXT,
    subnet_mask TEXT,
    gateway TEXT,
    dns_servers TEXT,
    is_active BOOLEAN DEFAULT 1,
    interface_type TEXT, -- 'Ethernet', 'WiFi', 'VPN'
    last_seen DATETIME DEFAULT CURRENT_TIMESTAMP,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Table: alerts
-- Store alert/notification history
CREATE TABLE IF NOT EXISTS alerts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    alert_type TEXT NOT NULL, -- 'NEW_CONNECTION', 'SUSPICIOUS_ACTIVITY', 'HIGH_BANDWIDTH'
    severity TEXT NOT NULL,   -- 'INFO', 'WARNING', 'ERROR', 'CRITICAL'
    title TEXT NOT NULL,
    message TEXT NOT NULL,
    connection_id INTEGER,
    process_id INTEGER,
    data JSON,               -- Additional alert data as JSON
    is_acknowledged BOOLEAN DEFAULT 0,
    acknowledged_at DATETIME,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (connection_id) REFERENCES connections(id) ON DELETE SET NULL
);

-- Table: configuration
-- Application configuration and settings
CREATE TABLE IF NOT EXISTS configuration (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    config_key TEXT NOT NULL UNIQUE,
    config_value TEXT,
    config_type TEXT DEFAULT 'string', -- 'string', 'integer', 'boolean', 'json'
    description TEXT,
    is_user_configurable BOOLEAN DEFAULT 1,
    last_modified DATETIME DEFAULT CURRENT_TIMESTAMP,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Indexes for performance optimization
CREATE INDEX IF NOT EXISTS idx_connections_pid ON connections(pid);
CREATE INDEX IF NOT EXISTS idx_connections_remote_ip ON connections(remote_ip);
CREATE INDEX IF NOT EXISTS idx_connections_service ON connections(microsoft_service);
CREATE INDEX IF NOT EXISTS idx_connections_established_time ON connections(established_time);
CREATE INDEX IF NOT EXISTS idx_connections_active ON connections(is_active);
CREATE INDEX IF NOT EXISTS idx_connections_state ON connections(connection_state);

CREATE INDEX IF NOT EXISTS idx_metrics_connection_id ON connection_metrics(connection_id);
CREATE INDEX IF NOT EXISTS idx_metrics_timestamp ON connection_metrics(timestamp);

CREATE INDEX IF NOT EXISTS idx_endpoints_service ON microsoft_endpoints(service_name);
CREATE INDEX IF NOT EXISTS idx_endpoints_active ON microsoft_endpoints(is_active);

CREATE INDEX IF NOT EXISTS idx_processes_name ON processes(name);
CREATE INDEX IF NOT EXISTS idx_processes_microsoft ON processes(is_microsoft_app);

CREATE INDEX IF NOT EXISTS idx_alerts_type ON alerts(alert_type);
CREATE INDEX IF NOT EXISTS idx_alerts_created ON alerts(created_at);
CREATE INDEX IF NOT EXISTS idx_alerts_acknowledged ON alerts(is_acknowledged);

-- Views for common queries
CREATE VIEW IF NOT EXISTS active_microsoft_connections AS
SELECT 
    c.id,
    c.pid,
    c.process_name,
    c.remote_ip,
    c.remote_port,
    c.remote_host,
    c.microsoft_service,
    c.service_category,
    c.bytes_sent,
    c.bytes_received,
    c.established_time,
    (c.bytes_sent + c.bytes_received) as total_bytes,
    (julianday('now') - julianday(c.established_time)) * 24 * 60 * 60 as duration_seconds
FROM connections c
WHERE c.is_active = 1 
    AND c.microsoft_service IS NOT NULL
ORDER BY c.established_time DESC;

CREATE VIEW IF NOT EXISTS connection_summary AS
SELECT 
    microsoft_service,
    service_category,
    COUNT(*) as connection_count,
    SUM(bytes_sent + bytes_received) as total_bytes,
    AVG(duration_ms) as avg_duration_ms,
    MIN(established_time) as first_connection,
    MAX(last_activity_time) as last_activity
FROM connections
WHERE microsoft_service IS NOT NULL
GROUP BY microsoft_service, service_category
ORDER BY total_bytes DESC;

CREATE VIEW IF NOT EXISTS process_network_summary AS
SELECT 
    p.pid,
    p.name as process_name,
    p.executable_path,
    COUNT(c.id) as connection_count,
    COUNT(CASE WHEN c.microsoft_service IS NOT NULL THEN 1 END) as microsoft_connections,
    SUM(c.bytes_sent + c.bytes_received) as total_bytes,
    GROUP_CONCAT(DISTINCT c.microsoft_service) as services_used
FROM processes p
LEFT JOIN connections c ON p.pid = c.pid
GROUP BY p.pid, p.name, p.executable_path
HAVING connection_count > 0
ORDER BY microsoft_connections DESC, total_bytes DESC;

-- Triggers for automatic updates
CREATE TRIGGER IF NOT EXISTS update_connection_timestamp 
    AFTER UPDATE ON connections
    FOR EACH ROW
    WHEN NEW.updated_at = OLD.updated_at
BEGIN
    UPDATE connections SET updated_at = CURRENT_TIMESTAMP WHERE id = NEW.id;
END;

CREATE TRIGGER IF NOT EXISTS update_process_last_seen
    AFTER INSERT ON connections
    FOR EACH ROW
BEGIN
    UPDATE processes SET last_seen = CURRENT_TIMESTAMP WHERE pid = NEW.pid;
END;

-- Insert default Microsoft endpoint definitions
INSERT OR IGNORE INTO microsoft_endpoints (ip_range, domain_pattern, service_name, service_category, description, priority) VALUES
-- Office 365 Core Services
('13.107.6.152/31', NULL, 'Office365', 'Productivity', 'Office 365 Core Services', 10),
('13.107.18.10/31', NULL, 'Office365', 'Productivity', 'Office 365 Core Services', 10),
('13.107.128.0/22', NULL, 'Office365', 'Productivity', 'Office 365 Core Services', 10),
('23.103.160.0/20', NULL, 'Office365', 'Productivity', 'Office 365 Core Services', 10),
('40.96.0.0/13', NULL, 'Office365', 'Productivity', 'Office 365 Core Services', 10),
('40.104.0.0/15', NULL, 'Office365', 'Productivity', 'Office 365 Core Services', 10),
('52.96.0.0/14', NULL, 'Office365', 'Productivity', 'Office 365 Core Services', 10),
('131.253.33.215/32', NULL, 'Office365', 'Productivity', 'Office 365 Core Services', 10),
('132.245.0.0/16', NULL, 'Office365', 'Productivity', 'Office 365 Core Services', 10),
('150.171.32.0/22', NULL, 'Office365', 'Productivity', 'Office 365 Core Services', 10),
('204.79.197.215/32', NULL, 'Office365', 'Productivity', 'Office 365 Core Services', 10),

-- Teams
('52.108.0.0/14', NULL, 'Teams', 'Communication', 'Microsoft Teams', 20),
('52.112.0.0/14', NULL, 'Teams', 'Communication', 'Microsoft Teams', 20),
('52.120.0.0/14', NULL, 'Teams', 'Communication', 'Microsoft Teams', 20),
('20.190.128.0/18', NULL, 'Teams', 'Communication', 'Microsoft Teams', 20),

-- Azure
('20.0.0.0/8', NULL, 'Azure', 'Cloud', 'Microsoft Azure', 5),
('13.64.0.0/11', NULL, 'Azure', 'Cloud', 'Microsoft Azure', 5),
('40.64.0.0/10', NULL, 'Azure', 'Cloud', 'Microsoft Azure', 5),
('52.0.0.0/8', NULL, 'Azure', 'Cloud', 'Microsoft Azure', 5),
('104.40.0.0/13', NULL, 'Azure', 'Cloud', 'Microsoft Azure', 5),
('137.116.0.0/14', NULL, 'Azure', 'Cloud', 'Microsoft Azure', 5),
('168.61.0.0/16', NULL, 'Azure', 'Cloud', 'Microsoft Azure', 5),

-- Windows Update
('13.107.4.50/32', NULL, 'WindowsUpdate', 'System', 'Windows Update', 15),
('52.109.8.0/22', NULL, 'WindowsUpdate', 'System', 'Windows Update', 15),
('52.109.12.0/22', NULL, 'WindowsUpdate', 'System', 'Windows Update', 15),

-- Domain-based patterns
(NULL, '*.microsoft.com', 'Microsoft', 'General', 'Microsoft Services', 30),
(NULL, '*.microsoftonline.com', 'Authentication', 'Identity', 'Microsoft Online Authentication', 25),
(NULL, '*.office.com', 'Office365', 'Productivity', 'Office 365 Services', 25),
(NULL, '*.office365.com', 'Office365', 'Productivity', 'Office 365 Services', 25),
(NULL, '*.outlook.com', 'Outlook', 'Email', 'Outlook Email Services', 25),
(NULL, '*.live.com', 'LiveServices', 'General', 'Microsoft Live Services', 20),
(NULL, '*.hotmail.com', 'Outlook', 'Email', 'Hotmail Email Services', 25),
(NULL, '*.skype.com', 'Skype', 'Communication', 'Skype Services', 25),
(NULL, '*.teams.microsoft.com', 'Teams', 'Communication', 'Microsoft Teams', 30),
(NULL, '*.sharepoint.com', 'SharePoint', 'Storage', 'SharePoint Services', 25),
(NULL, '*.onedrive.com', 'OneDrive', 'Storage', 'OneDrive Cloud Storage', 25),
(NULL, '*.azure.com', 'Azure', 'Cloud', 'Microsoft Azure', 25),
(NULL, '*.azurewebsites.net', 'Azure', 'Cloud', 'Azure Web Services', 20),
(NULL, '*.windowsupdate.com', 'WindowsUpdate', 'System', 'Windows Update', 25),
(NULL, '*.xbox.com', 'Xbox', 'Gaming', 'Xbox Live Services', 20),
(NULL, '*.bing.com', 'Bing', 'Search', 'Bing Search Services', 20),
(NULL, '*.msn.com', 'MSN', 'Portal', 'MSN Portal Services', 15);

-- Insert default configuration values
INSERT OR IGNORE INTO configuration (config_key, config_value, config_type, description) VALUES
('monitoring.polling_interval_ms', '5000', 'integer', 'Polling interval for connection monitoring in milliseconds'),
('monitoring.enable_etw', 'true', 'boolean', 'Enable Event Tracing for Windows'),
('monitoring.microsoft_only', 'true', 'boolean', 'Monitor only Microsoft endpoints'),
('monitoring.max_history_days', '30', 'integer', 'Maximum days to keep historical data'),
('alerts.enable_new_connection_alerts', 'true', 'boolean', 'Enable alerts for new connections'),
('alerts.enable_high_bandwidth_alerts', 'true', 'boolean', 'Enable alerts for high bandwidth usage'),
('alerts.bandwidth_threshold_mbps', '100', 'integer', 'Bandwidth threshold for alerts in Mbps'),
('database.auto_cleanup_enabled', 'true', 'boolean', 'Enable automatic database cleanup'),
('database.cleanup_interval_hours', '24', 'integer', 'Cleanup interval in hours'),
('ui.refresh_interval_ms', '1000', 'integer', 'UI refresh interval in milliseconds'),
('ui.max_connections_display', '1000', 'integer', 'Maximum connections to display in UI');

-- Insert initial monitoring session
INSERT INTO monitoring_sessions (computer_name, windows_version, service_version, session_notes)
VALUES ('DESKTOP-PC', 'Windows 11', '1.0.0', 'Initial monitoring session');
