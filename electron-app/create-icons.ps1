# Create basic application icons
Add-Type -AssemblyName System.Drawing

# Create 256x256 icon
$bitmap = New-Object System.Drawing.Bitmap(256, 256)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

# Fill background with gradient
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    [System.Drawing.Point]::new(0, 0),
    [System.Drawing.Point]::new(256, 256),
    [System.Drawing.Color]::FromArgb(59, 130, 246),
    [System.Drawing.Color]::FromArgb(37, 99, 235)
)
$graphics.FillRectangle($brush, 0, 0, 256, 256)

# Draw monitor symbol
$whiteBrush = [System.Drawing.Brushes]::White
$graphics.FillRectangle($whiteBrush, 40, 60, 176, 120)
$blackBrush = [System.Drawing.Brushes]::Black
$graphics.FillRectangle($blackBrush, 50, 70, 156, 100)

# Draw signal waves
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 4)
$graphics.DrawArc($pen, 180, 90, 40, 40, 0, 180)
$graphics.DrawArc($pen, 190, 100, 20, 20, 0, 180)

# Draw base
$graphics.FillRectangle($whiteBrush, 110, 180, 36, 40)
$graphics.FillRectangle($whiteBrush, 80, 210, 96, 20)

$graphics.Dispose()
$brush.Dispose()
$pen.Dispose()

# Save as ICO and PNG
$bitmap.Save("$PSScriptRoot\..\assets\icon.png", [System.Drawing.Imaging.ImageFormat]::Png)

# Create ICO file (simplified)
$iconSizes = @(16, 32, 48, 64, 128, 256)
foreach ($size in $iconSizes) {
    $resized = New-Object System.Drawing.Bitmap($bitmap, $size, $size)
    $resized.Save("$PSScriptRoot\..\assets\icon-$size.png", [System.Drawing.Imaging.ImageFormat]::Png)
    $resized.Dispose()
}

$bitmap.Dispose()

Write-Host "✅ Application icons created in electron-app\assets\" -ForegroundColor Green
