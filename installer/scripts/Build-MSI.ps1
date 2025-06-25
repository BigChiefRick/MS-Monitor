# Advanced MSI Builder using WiX Toolset
# Uncomment and run if you have WiX Toolset installed

<#
# Download WiX Toolset from: https://wixtoolset.org/
# This creates a professional Windows Installer (.msi) file

Write-Host "Building MSI installer with WiX..." -ForegroundColor Green

# Create WiX source file
$wixSource = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
  <Product Id="*" 
           Name="Microsoft Endpoint Monitor" 
           Language="1033" 
           Version="1.0.0" 
           Manufacturer="BigChiefRick" 
           UpgradeCode="12345678-1234-1234-1234-123456789012">
    
    <Package InstallerVersion="200" 
             Compressed="yes" 
             InstallScope="perMachine" 
             Description="Real-time monitoring for Microsoft services" />

    <MajorUpgrade DowngradeErrorMessage="A newer version is already installed." />
    <MediaTemplate EmbedCab="yes" />

    <Feature Id="ProductFeature" Title="Microsoft Endpoint Monitor" Level="1">
      <ComponentGroupRef Id="ProductComponents" />
    </Feature>
  </Product>

  <Fragment>
    <Directory Id="TARGETDIR" Name="SourceDir">
      <Directory Id="ProgramFilesFolder">
        <Directory Id="INSTALLFOLDER" Name="Microsoft Endpoint Monitor" />
      </Directory>
    </Directory>
  </Fragment>

  <Fragment>
    <ComponentGroup Id="ProductComponents" Directory="INSTALLFOLDER">
      <!-- Add your application files here -->
      <Component Id="MainExecutable" Guid="*">
        <File Id="MainExe" Source="path\to\your\executable.exe" KeyPath="yes" />
      </Component>
    </ComponentGroup>
  </Fragment>
</Wix>
"@

$wixSource | Out-File -FilePath "installer\MSMonitor.wxs" -Encoding UTF8

# Build commands (requires WiX installed)
# candle.exe installer\MSMonitor.wxs -out installer\build\
# light.exe installer\build\MSMonitor.wixobj -out installer\build\MSMonitor.msi

Write-Host "WiX source created at installer\MSMonitor.wxs" -ForegroundColor Green
Write-Host "Install WiX Toolset and run the candle/light commands to build MSI" -ForegroundColor Yellow
#>

Write-Host "WiX MSI builder script created (commented out)" -ForegroundColor Cyan
Write-Host "Uncomment and install WiX Toolset for advanced MSI creation" -ForegroundColor Yellow
