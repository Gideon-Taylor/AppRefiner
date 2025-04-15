# Installation Guide

This guide provides instructions for installing AppRefiner as a companion application to enhance your PeopleSoft Application Designer experience.

## Prerequisites

Before installing AppRefiner, ensure your system meets the following requirements:

- Windows operating system
- .NET 8 Runtime or SDK
- PeopleSoft Application Designer installed and functioning

## Installation Methods

### Manual Installation

1. Download the latest AppRefiner ZIP package from the [GitHub releases page](https://github.com/Gideon-Taylor/AppRefiner/releases/). Note that these releases provide **framework-dependent** builds. Users requiring a self-contained build will need to build the application from source.
2. Extract the ZIP file to a location of your choice (e.g., `C:\Program Files\AppRefiner`)
3. Create shortcuts to AppRefiner.exe as needed
4. (Optional) Add the installation directory to your system PATH

## First-Time Setup

After installing AppRefiner:

1. Launch PeopleSoft Application Designer
2. Start AppRefiner
3. AppRefiner will automatically detect Application Designer and begin enhancing its functionality
4. Press `Ctrl+Shift+P` in Application Designer to open the Command Palette and access AppRefiner features

## Editor Snapshots

AppRefiner automatically maintains a history of recent changes to definitions you work on, providing a safety net for your code. Instead of Git, it uses a local SQLite database to store snapshots.

- **Automatic Snapshots**: Every time you save a definition (PeopleCode, HTML, SQL, Freeform Stylesheets) in Application Designer while AppRefiner is running, a snapshot of the content is saved to the database.
- **Snapshot Limit**: The system keeps the last 'N' snapshots for each definition, automatically removing older ones beyond this configurable limit.
- **Reverting Changes**:
  - To revert a definition to a previous state, use the `Snapshot: Revert File` command from the command palette (`Ctrl+Shift+P`).
  - A window will appear displaying the recent snapshots available for the current editor.
  - You can view the full content of a snapshot or see a diff comparing the snapshot to the current editor content before choosing to revert.

## Next Steps

After installing AppRefiner, check out the [User Guide](../user-guide/README.md) for more information on how to use AppRefiner.
