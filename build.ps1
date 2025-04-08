param(
    [switch]$SelfContained = $false
)

# Build configuration
$Configuration = "Release"
$Platform = "x64"
$OutputDir = "publish"
$FrameworkOutputDir = Join-Path $OutputDir "framework"
$SelfContainedOutputDir = Join-Path $OutputDir "self-contained"

# Ensure we're in the correct directory (script directory)
Set-Location $PSScriptRoot

Write-Host "Starting AppRefiner build process..."

# Build requirements check
function Test-BuildRequirements {
    Write-Host "Checking build requirements..."
    
    # Check for .NET SDK
    try {
        $dotnetVersion = dotnet --version
        Write-Host ".NET SDK found: $dotnetVersion"
    } catch {
        Write-Error "Error: .NET SDK not found. Please install .NET 8 SDK."
        return $false
    }
    
    # Check for MSBuild (Visual Studio)
    try {
        $vsPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if (-not $vsPath) {
            Write-Error "Error: Visual Studio with MSBuild not found. Please install Visual Studio 2022 with C++ development tools."
            return $false
        }
        $msbuildPath = Join-Path $vsPath "MSBuild\Current\Bin\MSBuild.exe"
        if (-not (Test-Path $msbuildPath)) {
            Write-Error "Error: MSBuild not found at expected location. Please install Visual Studio 2022 with C++ development tools."
            return $false
        }
        Write-Host "MSBuild found at: $msbuildPath"
    } catch {
        Write-Error "Error checking for Visual Studio: $_"
        return $false
    }
    
    # Check for Java (required for ANTLR)
    try {
        $javaVersion = & java -version 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Error: Java not found. Please install Java 17 or later and ensure it's available in your PATH."
            return $false
        }
        Write-Host "Java found: $($javaVersion[0])"
    } catch {
        Write-Error "Error: Java not found. Please install Java 17 or later and ensure it's available in your PATH."
        return $false
    }
    
    return $true
}

# Restore dependencies
function Restore-Dependencies {
    Write-Host "Restoring NuGet dependencies..."
    dotnet restore
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error restoring dependencies."
        exit $LASTEXITCODE
    }
}

# Build C++ Hook DLL
function Build-HookDll {
    Write-Host "Building AppRefinerHook (C++) for $Configuration|$Platform..."
    
    # Find MSBuild
    $vsPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    $msbuildPath = Join-Path $vsPath "MSBuild\Current\Bin\MSBuild.exe"
    
    # Build the C++ project
    & "$msbuildPath" "AppRefinerHook\AppRefinerHook.vcxproj" "/p:Configuration=$Configuration" "/p:Platform=$Platform"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error building AppRefinerHook."
        exit $LASTEXITCODE
    }
}

# Build AppRefiner
function Build-AppRefiner {
    param(
        [bool]$IsSelfContained
    )
    
    $targetDir = if ($IsSelfContained) { $SelfContainedOutputDir } else { $FrameworkOutputDir }
    $selfContainedValue = if ($IsSelfContained) { "true" } else { "false" }
    
    Write-Host "Building AppRefiner (.NET) for win-$Platform in $Configuration mode..."
    Write-Host "Publishing to: $targetDir (Self-contained: $IsSelfContained)"
    
    dotnet publish "AppRefiner/AppRefiner.csproj" /p:SelfContained=$selfContainedValue -r "win-$Platform" -c $Configuration -o $targetDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error building AppRefiner."
        exit $LASTEXITCODE
    }
}

# Copy Hook DLL to output directory
function Copy-HookDll {
    param(
        [string]$DestinationDir
    )
    
    $sourcePath = "AppRefinerHook\x64\$Configuration\AppRefinerHook.dll"
    $destinationPath = Join-Path $DestinationDir "AppRefinerHook.dll"
    
    Write-Host "Copying AppRefinerHook.dll to $destinationPath..."
    
    if (-not (Test-Path $sourcePath)) {
        Write-Error "Error: AppRefinerHook.dll not found at $sourcePath"
        exit 1
    }
    
    Copy-Item -Path $sourcePath -Destination $destinationPath -Force
}

# Create release ZIP
function Create-ReleaseZip {
    param(
        [string]$SourceDir,
        [bool]$IsSelfContained
    )
    
    $date = Get-Date -Format "yyyy-MM-dd"
    $suffix = if ($IsSelfContained) { "self-contained" } else { "framework-dependent" }
    $zipFileName = "AppRefiner-$date-$suffix.zip"
    
    Write-Host "Creating release ZIP: $zipFileName..."
    
    if (Test-Path $zipFileName) {
        Remove-Item $zipFileName -Force
    }
    
    Compress-Archive -Path "$SourceDir\*" -DestinationPath $zipFileName
    
    Write-Host "Release ZIP created at: $zipFileName"
    return $zipFileName
}

# Main build process
if (-not (Test-BuildRequirements)) {
    exit 1
}

# Create output directories if they don't exist
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$targetDir = if ($SelfContained) { $SelfContainedOutputDir } else { $FrameworkOutputDir }
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir | Out-Null
}

# Execute build steps
Restore-Dependencies
Build-HookDll
Build-AppRefiner -IsSelfContained $SelfContained
Copy-HookDll -DestinationDir $targetDir
$zipFile = Create-ReleaseZip -SourceDir $targetDir -IsSelfContained $SelfContained

Write-Host ""
Write-Host "Build completed successfully!"
Write-Host "Release package: $zipFile"
Write-Host ""
Write-Host "To run AppRefiner, extract the ZIP and run AppRefiner.exe" 