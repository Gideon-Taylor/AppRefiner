param(
    [switch]$SelfContained = $false,
    [string]$Version = "",  # Allow manual version override
    [string]$SigningKeyPath = ""  # Path to strong name key file (.snk)
)

# Get the next semantic version
function Get-NextVersion {
    # Fetch tags from remote to ensure we have the latest
    Write-Host "Fetching tags from remote..."
    git fetch --tags --quiet 2>$null

    # Get all tags matching x.x.x pattern
    $tags = git tag -l | Where-Object { $_ -match '^\d+\.\d+\.\d+$' }

    if (-not $tags) {
        # No previous semantic version tags, use default
        Write-Host "No previous semantic version tags found, using default: 1.1.0"
        return "1.1.0"
    }

    # Parse and sort versions
    $versions = $tags | ForEach-Object {
        $parts = $_.Split('.')
        [PSCustomObject]@{
            Original = $_
            Major = [int]$parts[0]
            Minor = [int]$parts[1]
            Build = [int]$parts[2]
        }
    } | Sort-Object Major, Minor, Build -Descending

    # Get the latest version
    $latest = $versions[0]
    Write-Host "Latest version: $($latest.Original)"

    # Increment version
    $newBuild = $latest.Build + 1
    $newMinor = $latest.Minor
    $newMajor = $latest.Major

    # Handle build overflow: 1.0.9 → 1.1.0
    if ($newBuild -gt 9) {
        $newBuild = 0
        $newMinor++

        # Handle minor overflow: 1.9.0 → 2.0.0
        if ($newMinor -gt 9) {
            $newMinor = 0
            $newMajor++
        }
    }

    $newVersion = "$newMajor.$newMinor.$newBuild"
    Write-Host "Next version: $newVersion"
    return $newVersion
}

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
        [bool]$IsSelfContained,
        [string]$Version,
        [string]$SigningKeyPath
    )

    $targetDir = if ($IsSelfContained) { $SelfContainedOutputDir } else { $FrameworkOutputDir }
    $selfContainedValue = if ($IsSelfContained) { "true" } else { "false" }

    Write-Host "Building AppRefiner (.NET) for win-$Platform in $Configuration mode..."
    Write-Host "Publishing to: $targetDir (Self-contained: $IsSelfContained)"
    Write-Host "Version: $Version"

    # Build base arguments
    $buildArgs = @(
        "publish",
        "AppRefiner/AppRefiner.csproj",
        "/p:SelfContained=$selfContainedValue",
        "/p:AssemblyVersion=$Version",
        "/p:FileVersion=$Version",
        "/p:InformationalVersion=$Version",
        "-r", "win-$Platform",
        "-c", $Configuration,
        "-o", $targetDir
    )

    # Add signing parameters if key path is provided
    if (-not [string]::IsNullOrWhiteSpace($SigningKeyPath)) {
        if (Test-Path $SigningKeyPath) {
            Write-Host "Strong name signing enabled with key: $SigningKeyPath"
            $buildArgs += "/p:SignAssembly=true"
            $buildArgs += "/p:AssemblyOriginatorKeyFile=$SigningKeyPath"
        } else {
            Write-Warning "Signing key file not found at: $SigningKeyPath - building without signing"
        }
    }

    & dotnet $buildArgs
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
        [bool]$IsSelfContained,
        [string]$Version
    )

    $suffix = if ($IsSelfContained) { "self-contained" } else { "framework-dependent" }
    $zipFileName = "AppRefiner-$Version-$suffix.zip"

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

# Get version
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-NextVersion
}

# Execute build steps
Restore-Dependencies
Build-HookDll
Build-AppRefiner -IsSelfContained $SelfContained -Version $Version -SigningKeyPath $SigningKeyPath
Copy-HookDll -DestinationDir $targetDir
$zipFile = Create-ReleaseZip -SourceDir $targetDir -IsSelfContained $SelfContained -Version $Version

Write-Host ""
Write-Host "Build completed successfully!"
Write-Host "Version: $Version"
Write-Host "Release package: $zipFile"
Write-Host ""
Write-Host "To run AppRefiner, extract the ZIP and run AppRefiner.exe" 