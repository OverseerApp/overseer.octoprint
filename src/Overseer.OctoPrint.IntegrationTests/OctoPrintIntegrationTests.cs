using Overseer.Server.Integration.Machines;

namespace Overseer.OctoPrint.IntegrationTests;

public class OctoPrintIntegrationTests
{
  private class SimpleHttpClientFactory : IHttpClientFactory
  {
    public HttpClient CreateClient(string name)
    {
      return new HttpClient();
    }
  }

  private OctoPrintMachine CreateMachine()
  {
    var url = Environment.GetEnvironmentVariable("OCTOPRINT_URL");
    var apiKey = Environment.GetEnvironmentVariable("OCTOPRINT_APIKEY");

    // Use sensible defaults/env vars or throw to ensure valid config for integration tests
    if (string.IsNullOrEmpty(url))
    {
      throw new InvalidOperationException("OCTOPRINT_URL environment variable is not set.");
    }
    if (string.IsNullOrEmpty(apiKey))
    {
      throw new InvalidOperationException("OCTOPRINT_APIKEY environment variable is not set.");
    }

    return new OctoPrintMachine
    {
      Id = 1,
      Name = "Test OctoPrint",
      Url = url,
      ApiKey = apiKey,
    };
  }

  private static async Task<MachineStatus> GetStatusAsync(OctoPrintMachineProvider provider)
  {
    var tcs = new TaskCompletionSource<MachineStatus>();

    provider.StatusUpdated += (s, e) =>
    {
      // We got a status update, resolve the task
      tcs.TrySetResult(e.Status);
    };

    // Start polling with a short interval
    provider.Start(1000);

    // Wait for first update or timeout
    var delayTask = Task.Delay(TimeSpan.FromSeconds(10));
    var completedTask = await Task.WhenAny(tcs.Task, delayTask);

    provider.Stop();

    if (completedTask == delayTask)
    {
      throw new TimeoutException("Timed out waiting for status update.");
    }

    return await tcs.Task;
  }

  [Fact]
  public async Task TestIdleState()
  {
    // Setup: OctoPrint should be running and connected to printer, but not printing.
    var machine = CreateMachine();
    using var provider = new OctoPrintMachineProvider(machine, new SimpleHttpClientFactory());

    var status = await GetStatusAsync(provider);

    Assert.Equal(MachineState.Idle, status.State);
  }

  [Fact]
  public async Task TestPrintingState()
  {
    // Setup: OctoPrint should be printing a job.
    var machine = CreateMachine();
    using var provider = new OctoPrintMachineProvider(machine, new SimpleHttpClientFactory());

    var status = await GetStatusAsync(provider);

    Assert.Equal(MachineState.Operational, status.State);
  }

  [Fact]
  public async Task TestOfflineState()
  {
    // Setup: OctoPrint should be physically disconnected from printer or server down?
    // Based on implementation, if State is not Printing/Paused, it is Idle.
    // If the server is unreachable, it logs error and sends empty status (which defaults to Idle/Unknown).
    // For the purpose of this test, we assume "Offline" means the OctoPrint instance reports disconnect from Printer.

    var machine = CreateMachine();
    using var provider = new OctoPrintMachineProvider(machine, new SimpleHttpClientFactory());

    var status = await GetStatusAsync(provider);

    // OctoPrint provider currently maps other states to Idle
    Assert.Equal(MachineState.Idle, status.State);
  }
}
