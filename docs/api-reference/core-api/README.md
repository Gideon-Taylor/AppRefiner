# Core API

The Core API provides the fundamental interfaces and classes that form the backbone of AppRefiner. This section documents the primary APIs that developers can use to interact with AppRefiner's core functionality.

## Overview

AppRefiner's Core API is organized into several key components:

- **Linter API**: Interfaces and classes for code analysis and issue detection
- **Refactor API**: Tools for automated code transformations
- **Styler API**: Components for code formatting and style analysis
- **Database API**: Interfaces for interacting with PeopleSoft databases

## Common Patterns

Throughout the Core API, you'll find several common design patterns:

1. **Listener Pattern**: Many components use the listener pattern to respond to events
2. **Builder Pattern**: Complex objects are often constructed using builder classes
3. **Strategy Pattern**: Algorithms are encapsulated in interchangeable strategy classes
4. **Factory Pattern**: Object creation is handled by factory classes

## Getting Started

If you're new to the AppRefiner API, we recommend starting with the [Linter API](linter-api.md) documentation, as it provides a good introduction to the overall architecture and patterns used throughout the codebase.

## API Stability

The Core API is considered stable and follows semantic versioning. Breaking changes will only be introduced in major version updates.

## Next Steps

Explore the specific API documentation:

- [Linter API](linter-api.md)
- [Refactor API](refactor-api.md)
- [Styler API](styler-api.md)
- [Database API](database-api.md)
