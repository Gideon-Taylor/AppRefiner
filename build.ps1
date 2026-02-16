param(
    [switch]$SelfContained = $false,
    [switch]$Clean = $false,
    [string]$Version = "",  # Allow manual version override
    [string]$SigningKeyPath = "",  # Path to strong name key file (.snk)
    [string]$SignToolPath = "",  # Path to signtool.exe
    [string]$SignDlibPath = "",  # Path to Azure.CodeSigning.Dlib.dll
    [string]$SignMetadataPath = ""  # Path to signing metadata JSON
)

# Get the latest semantic version tag
function Get-LatestVersion {
    # Fetch tags from remote to ensure we have the latest
    Write-Host "Fetching tags from remote..."
    git fetch --tags --quiet 2>$null

    # Get all tags matching x.x.x pattern
    $tags = git tag -l | Where-Object { $_ -match '^\d+\.\d+\.\d+$' }

    if (-not $tags) {
        Write-Error "No semantic version tags found. Please create a tag first (e.g., git tag 1.0.0)."
        exit 1
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
    Write-Host "Using version from latest tag: $($latest.Original)"
    return $latest.Original
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

# Build Scintilla Mods
function Build-ScintillaMods {
    param(
        [string]$DestinationDir
    )

    $scintillaWin32 = Join-Path $PSScriptRoot "..\Scintilla\win32"
    if (-not (Test-Path $scintillaWin32)) {
        Write-Error "Error: Scintilla repo not found at $scintillaWin32"
        exit 1
    }

    $scintillaWin32 = Resolve-Path $scintillaWin32

    # Save the current branch so we can restore it afterwards
    $originalBranch = git -C $scintillaWin32 rev-parse --abbrev-ref HEAD

    # Define branch -> build info mapping
    $branches = @(
        @{ Branch = "4-4-6-mods"; Version = "4.4.6.0"; Project = "SciLexer.vcxproj"; Artifact = "SciLexer.dll" },
        @{ Branch = "5-3-3-mods"; Version = "5.3.3.0"; Project = "Scintilla.vcxproj"; Artifact = "Scintilla.dll" },
        @{ Branch = "5-5-0-mods"; Version = "5.5.0.0"; Project = "Scintilla.vcxproj"; Artifact = "Scintilla.dll" }
    )

    $modsDir = Join-Path $DestinationDir "scintilla_mods"

    foreach ($entry in $branches) {
        Write-Host ""
        Write-Host "Building Scintilla mod: $($entry.Branch) -> $($entry.Version)..."

        # Checkout the branch
        git -C $scintillaWin32 checkout $entry.Branch --quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Error: Failed to checkout branch $($entry.Branch)"
            exit 1
        }

        # Build
        $projectPath = Join-Path $scintillaWin32 $entry.Project
        & msbuild $projectPath /p:Configuration=$Configuration /p:Platform=$Platform /verbosity:minimal
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Error building Scintilla mod $($entry.Branch)."
            exit 1
        }

        # Copy artifact to versioned folder
        $versionDir = Join-Path $modsDir $entry.Version
        if (-not (Test-Path $versionDir)) {
            New-Item -ItemType Directory -Path $versionDir | Out-Null
        }

        $artifactPath = Join-Path $scintillaWin32 "x64\Release\$($entry.Artifact)"
        if (-not (Test-Path $artifactPath)) {
            Write-Error "Error: Build artifact not found at $artifactPath"
            exit 1
        }

        Copy-Item -Path $artifactPath -Destination (Join-Path $versionDir $entry.Artifact) -Force
        Write-Host "Copied $($entry.Artifact) to scintilla_mods/$($entry.Version)/"
    }

    # Restore original branch
    Write-Host ""
    Write-Host "Restoring Scintilla repo to branch: $originalBranch"
    git -C $scintillaWin32 checkout $originalBranch --quiet
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

# Digitally sign binaries
function Invoke-Signing {
    param(
        [string]$TargetDir
    )

    if ([string]::IsNullOrWhiteSpace($SignToolPath) -or
        [string]::IsNullOrWhiteSpace($SignDlibPath) -or
        [string]::IsNullOrWhiteSpace($SignMetadataPath)) {
        Write-Host "Skipping code signing (signing parameters not provided)."
        return
    }

    foreach ($path in @($SignToolPath, $SignDlibPath, $SignMetadataPath)) {
        if (-not (Test-Path $path)) {
            Write-Error "Error: Signing dependency not found at $path"
            exit 1
        }
    }

    Write-Host ""
    Write-Host "Signing binaries..."

    # Core AppRefiner binaries
    $filesToSign = @(
        (Join-Path $TargetDir "AppRefiner.dll"),
        (Join-Path $TargetDir "AppRefiner.exe"),
        (Join-Path $TargetDir "AppRefinerHook.dll"),
        (Join-Path $TargetDir "PeopleCodeParser.SelfHosted.dll"),
        (Join-Path $TargetDir "PeopleCodeTypeInfo.dll")
    )

    # Scintilla mod DLLs
    $modsDir = Join-Path $TargetDir "scintilla_mods"
    if (Test-Path $modsDir) {
        $modDlls = Get-ChildItem -Path $modsDir -Filter "*.dll" -Recurse
        foreach ($dll in $modDlls) {
            $filesToSign += $dll.FullName
        }
    }

    # Verify all files exist
    foreach ($file in $filesToSign) {
        if (-not (Test-Path $file)) {
            Write-Error "Error: File to sign not found: $file"
            exit 1
        }
    }

    Write-Host "Signing $($filesToSign.Count) files..."

    & $SignToolPath sign /v /fd SHA256 `
        /tr "http://timestamp.acs.microsoft.com" /td SHA256 `
        /dlib $SignDlibPath `
        /dmdf $SignMetadataPath `
        $filesToSign

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Error signing binaries."
        exit $LASTEXITCODE
    }

    Write-Host "Signing completed successfully."
}

# Clean build artifacts
function Invoke-Clean {
    Write-Host "Cleaning build artifacts..."

    # Remove publish output directory
    if (Test-Path $OutputDir) {
        Write-Host "Removing $OutputDir..."
        Remove-Item -Path $OutputDir -Recurse -Force
    }

    # Remove C++ hook build artifacts
    $hookBuildDir = "AppRefinerHook\x64"
    if (Test-Path $hookBuildDir) {
        Write-Host "Removing $hookBuildDir..."
        Remove-Item -Path $hookBuildDir -Recurse -Force
    }

    # Remove release ZIP files
    $zipFiles = Get-ChildItem -Path "." -Filter "AppRefiner-*.zip" -ErrorAction SilentlyContinue
    foreach ($zip in $zipFiles) {
        Write-Host "Removing $($zip.Name)..."
        Remove-Item -Path $zip.FullName -Force
    }

    Write-Host "Clean completed."
}

# Handle clean-only invocation
if ($Clean) {
    Set-Location $PSScriptRoot
    Invoke-Clean
    exit 0
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
    $Version = Get-LatestVersion
}

# Execute build steps
Restore-Dependencies
Build-HookDll
Build-AppRefiner -IsSelfContained $SelfContained -Version $Version -SigningKeyPath $SigningKeyPath
Copy-HookDll -DestinationDir $targetDir
Build-ScintillaMods -DestinationDir $targetDir
Invoke-Signing -TargetDir $targetDir
$zipFile = Create-ReleaseZip -SourceDir $targetDir -IsSelfContained $SelfContained -Version $Version

Write-Host ""
Write-Host "Build completed successfully!"
Write-Host "Version: $Version"
Write-Host "Release package: $zipFile"
Write-Host ""
Write-Host "To run AppRefiner, extract the ZIP and run AppRefiner.exe" 