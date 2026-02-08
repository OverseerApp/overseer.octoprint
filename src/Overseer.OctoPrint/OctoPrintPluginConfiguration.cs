using Microsoft.Extensions.DependencyInjection;
using Overseer.OctoPrint.Channels;
using Overseer.OctoPrint.Machines;
using Overseer.OctoPrint.Machines.Octoprint;
using Overseer.OctoPrint.Models;
using Overseer.Server.Integration;

namespace Overseer.OctoPrint;

public class OctoPrintPluginConfiguration : IPluginConfiguration
{
  public void ConfigureServices(IServiceCollection services)
  {
    services.AddTransient<IMachineProvider<OctoprintMachine>, OctoprintMachineProvider>();
    services.AddSingleton<IMachineStatusChannel, DefaultMachineStatusChannel>();
  }
}

public class DefaultMachineStatusChannel : IMachineStatusChannel
{
  public Task WriteAsync(MachineStatus status, CancellationToken cancellationToken = default)
  {
    return Task.CompletedTask;
  }
}
