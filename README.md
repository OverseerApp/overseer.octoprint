# OctoPrint Machine Provider

## Overview

This plugin integrates [OctoPrint](https://octoprint.org/) with Overseer, enabling comprehensive monitoring and control of 3D printers running OctoPrint. OctoPrint is a web-based interface and API for 3D printer control and monitoring, supporting a wide range of printer models and hardware configurations.

## Features

- Real-time printer status monitoring
- Temperature tracking for all heaters (bed, hotends)
- Print job progress and time estimates
- Printer control (pause, resume, cancel)
- Webcam stream integration with orientation support
- Automatic status polling with intelligent error handling
- Client certificate authentication support

## Usage

### Initialize Machine Provider

```csharp
using Overseer.OctoPrint;
using Microsoft.Extensions.DependencyInjection;

// Get HttpClientFactory from DI
var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
var provider = new OctoPrintMachineProvider(machine, httpClientFactory);

// Configure machine settings
await provider.Configure(machine);

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
```

### Monitor Status

The provider automatically polls OctoPrint and raises the `StatusUpdated` event. Status includes:

- Current state (Idle, Operational, Paused, Offline)
- Temperature readings for all heaters
- Print progress percentage
- Elapsed time and time remaining
- Job information

## Architecture

### Core Components

- **OctoPrintMachineProvider**: Main provider implementing OctoPrint API communication

### Models

- **OctoPrintMachine**: Configuration for an OctoPrint instance
- **MachineStatus**: Current status snapshot
- **Job, Status, PrinterProfiles, Settings**: OctoPrint API response models

### Error Handling

The provider includes intelligent error handling:

- Offline mode after consecutive failures (5 attempts)
- Throttled updates (2-minute intervals) during extended outages

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
├── Models/                   # Core domain API models
├── OctoPrintMachine.cs
├── OctoPrintMachineProvider.cs
├── OctoPrintPluginConfiguration.cs
└── Overseer.OctoPrint.csproj
```

## Dependencies

- .NET 10.0
- Overseer.Server.Integration (1.1.0-rc.3)
- Microsoft.Extensions.Http (10.0.2)
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
