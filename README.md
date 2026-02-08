# Overseer.OctoPrint

OctoPrint machine provider plugin for [Overseer](https://github.com/OverseerApp/overseer) - a 3D printer monitoring and management application.

## Description

This plugin provides OctoPrint integration for Overseer, enabling monitoring and control of 3D printers running OctoPrint. It wraps the OctoPrint API implementation from the main Overseer repository as a standalone plugin.

## Features

- Real-time printer status monitoring
- Temperature tracking for all heaters (bed, hotends)
- Print job progress and time estimates
- Printer control (pause, resume, cancel)
- GCode command execution
- Webcam stream integration with orientation support
- Support for multiple printer profiles
- Automatic status polling with intelligent error handling
- Client certificate authentication support

## Installation

### NuGet Package

```bash
dotnet add package Overseer.OctoPrint --version 1.0.0
```

### From Source

```bash
git clone https://github.com/OverseerApp/overseer.octoprint.git
cd overseer.octoprint
dotnet build
```

## Configuration

### 1. Register the Plugin

Add the plugin to your Overseer server configuration:

```csharp
using Overseer.OctoPrint;
using Overseer.Server.Integration;

// In your service configuration
services.AddOverseerPlugin<OctoPrintPluginConfiguration>();
```

### 2. Configure OctoPrint Machine

Create an `OctoprintMachine` instance with the required settings:

```csharp
using Overseer.OctoPrint.Models;

var machine = new OctoprintMachine
{
    Name = "My OctoPrint Printer",
    Url = "http://octopi.local",  // Your OctoPrint URL
    ApiKey = "YOUR_OCTOPRINT_API_KEY",
    Disabled = false
};
```

### 3. API Key Setup

To obtain an OctoPrint API key:
1. Open OctoPrint web interface
2. Navigate to Settings → API
3. Copy the existing API key or create a new one

### 4. Optional Settings

```csharp
machine.ClientCertificate = "MyCertName";  // For TLS client authentication
machine.WebCamUrl = "http://octopi.local/webcam/?action=stream";  // Manual webcam URL
machine.WebCamOrientation = WebCamOrientation.FlippedHorizontally;
```

## Usage

### Initialize Machine Provider

```csharp
using Overseer.OctoPrint.Machines.Octoprint;
using Overseer.OctoPrint.Channels;

var statusChannel = serviceProvider.GetRequiredService<IMachineStatusChannel>();
var provider = new OctoprintMachineProvider(machine, statusChannel);

// Load configuration from OctoPrint
await provider.LoadConfiguration(machine);

// Start polling (interval in milliseconds)
provider.Start(2000);  // Poll every 2 seconds
```

### Control Printer

```csharp
// Pause current job
await provider.PauseJob();

// Resume job
await provider.ResumeJob();

// Cancel job
await provider.CancelJob();

// Execute custom GCode
await provider.ExecuteGcode("M104 S200");  // Set hotend temperature
```

### Monitor Status

The provider automatically polls OctoPrint and publishes status updates through `IMachineStatusChannel`. Status includes:
- Current state (Idle, Operational, Paused, Offline)
- Temperature readings for all heaters
- Print progress percentage
- Elapsed time and time remaining
- Job information

## Architecture

### Core Components

- **OctoprintMachineProvider**: Main provider implementing OctoPrint API communication
- **PollingMachineProvider**: Base class handling periodic status updates
- **MachineProvider**: Abstract base for all machine providers
- **IMachineStatusChannel**: Interface for publishing machine status updates

### Models

- **OctoprintMachine**: Configuration for an OctoPrint instance
- **MachineStatus**: Current status snapshot
- **Job, Status, PrinterProfiles, Settings**: OctoPrint API response models

### Error Handling

The provider includes intelligent error handling:
- Automatic retry with exponential backoff
- Offline mode after consecutive failures (5 attempts)
- Throttled updates (2-minute intervals) during extended outages
- Custom `OverseerException` for application-specific errors

## Development

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Project Structure

```
src/Overseer.OctoPrint/
├── Machines/
│   ├── Octoprint/
│   │   ├── Models/          # OctoPrint API models
│   │   └── OctoprintMachineProvider.cs
│   ├── IMachineProvider.cs
│   ├── MachineProvider.cs
│   └── PollingMachineProvider.cs
├── Models/                   # Core domain models
├── Channels/                 # Status update channels
└── OctoPrintPluginConfiguration.cs
```

## Dependencies

- .NET 10.0
- Overseer.Server.Integration (1.0.3)
- RestSharp (113.1.0)
- log4net (3.0.1)

## Related Projects

- [Overseer](https://github.com/OverseerApp/overseer) - Main application
- [Overseer.Integration](https://github.com/OverseerApp/overseer.integration) - Plugin SDK

## License

See [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Support

For issues and questions:
- GitHub Issues: https://github.com/OverseerApp/overseer.octoprint/issues
- Main Project: https://github.com/OverseerApp/overseer