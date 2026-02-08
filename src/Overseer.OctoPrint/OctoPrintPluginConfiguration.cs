using Microsoft.Extensions.DependencyInjection;
using Overseer.Server.Integration;
using Overseer.Server.Integration.Machines;

namespace Overseer.OctoPrint;

public class OctoPrintPluginConfiguration : IPluginConfiguration
{
  public void ConfigureServices(IServiceCollection services)
  {
    services.AddSingleton<MachineProviderFactory<OctoPrintMachine, OctoPrintMachineProvider>>(serviceProvider =>
    {
      return machine => ActivatorUtilities.CreateInstance<OctoPrintMachineProvider>(serviceProvider, machine);
    });
  }
}
