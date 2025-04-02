# Installation Guide

This guide provides instructions for installing AppRefiner as a companion application to enhance your PeopleSoft Application Designer experience.

## Prerequisites

Before installing AppRefiner, ensure your system meets the following requirements:

- Windows operating system
- .NET 8 Runtime or SDK
- PeopleSoft Application Designer installed and functioning

## Installation Methods

### Manual Installation

1. Download the latest AppRefiner ZIP package from the [official website](https://example.com/apprefiner/download) or [GitHub releases](https://github.com/example/apprefiner/releases)
2. Extract the ZIP file to a location of your choice (e.g., `C:\Program Files\AppRefiner`)
3. Create shortcuts to AppRefiner.exe as needed
4. (Optional) Add the installation directory to your system PATH

## First-Time Setup

After installing AppRefiner:

1. Launch PeopleSoft Application Designer
2. Start AppRefiner
3. AppRefiner will automatically detect Application Designer and begin enhancing its functionality
4. Press `Ctrl+Shift+P` in Application Designer to open the Command Palette and access AppRefiner features

## Version Control with Git

AppRefiner integrates with Git to provide powerful automated backups for various definitions:

- PeopleCode
- HTML
- SQL (both SQL objects and view SQL)
- Freeform Stylesheets

1. **Initialize a Git Repository**:
   - Click the "Git Repository..." button on the main AppRefiner window
   - Choose a location for your repository
   - AppRefiner will initialize a repository at the chosen location

2. **Tracking Changes**:
   - AppRefiner automatically tracks changes as definitions are opened/saved
   - AppRefiner leverages a single repository and separates defintions by database name and definition type.

3. **Reverting**:
   - To revert a file to a previous snapshot, you can access the Git: Revert file command from the command palette
   - A window will open showing you the previous snapshots you can return to
   - You can view a snapshots full content or you can see a diff showing what changes will happen to the current editor if you accept this snapshot.

## Next Steps

After installing AppRefiner, check out the [User Guide](../user-guide/README.md) for more information on how to use AppRefiner.
