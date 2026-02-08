# Integration Testing

This project includes integration tests that verify the interaction with an OctoPrint instance.
These tests require a running OctoPrint server and manual configuration of its state before running each test.

## Prerequisites

1.  **OctoPrint Server**: You need access to an OctoPrint server.
2.  **Environment Variables**: You must set the following environment variables before running the tests:
    - `OCTOPRINT_URL`: The base URL of your OctoPrint instance (e.g., `http://192.168.1.100`).
    - `OCTOPRINT_APIKEY`: Your OctoPrint API Key.

## Running the Tests

The tests are located in `Overseer.OctoPrint.IntegrationTests`.

### 1. TestIdleState

**Goal**: Verify the system detects the Idle state.
**Preparation**:

- Ensure OctoPrint is running and connected to the printer.
- Ensure the printer is **not** printing or paused.
- Run the test `TestIdleState`.

### 2. TestPrintingState

**Goal**: Verify the system detects the Printing state.
**Preparation**:

- Ensure OctoPrint is running and connected to the printer.
- Start a print job (you can print a dummy file or verify against a real print).
- Run the test `TestPrintingState`.

### 3. TestOfflineState

**Goal**: Verify the system handles the offline/disconnected state.
**Preparation**:

- Disconnect the printer from OctoPrint (click "Disconnect" in the Connection panel).
- OR stop the OctoPrint server (if testing server unreachable behavior).
- Run the test `TestOfflineState`.
- **Note**: Currently, the system reports `MachineState.Idle` for offline/disconnected states.

## Execution

You can run the tests using Visual Studio's Test Explorer or via the command line:

```bash
# Set environment variables (PowerShell example)
$env:OCTOPRINT_URL="http://your-octoprint-url"
$env:OCTOPRINT_APIKEY="your-api-key"

# Run tests
dotnet test tests/Overseer.OctoPrint.IntegrationTests
```
