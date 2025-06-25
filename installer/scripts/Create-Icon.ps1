# Create a simple icon for the application
# This creates a basic .ico file - replace with professional icon if needed

Add-Type -AssemblyName System.Drawing

$bitmap = New-Object System.Drawing.Bitmap(32, 32)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

# Fill background
$graphics.FillRectangle([System.Drawing.Brushes]::Blue, 0, 0, 32, 32)

# Draw monitor symbol
$graphics.FillRectangle([System.Drawing.Brushes]::White, 4, 6, 24, 16)
$graphics.FillRectangle([System.Drawing.Brushes]::Black, 6, 8, 20, 12)
$graphics.FillRectangle([System.Drawing.Brushes]::White, 14, 22, 4, 6)

$graphics.Dispose()

# Save as icon (basic implementation)
$bitmap.Save("$PSScriptRoot\..\assets\monitor.ico", [System.Drawing.Imaging.ImageFormat]::Icon)
$bitmap.Dispose()

Write-Host "Basic icon created at assets\monitor.ico" -ForegroundColor Green
Write-Host "Replace with professional icon for production use" -ForegroundColor Yellow
