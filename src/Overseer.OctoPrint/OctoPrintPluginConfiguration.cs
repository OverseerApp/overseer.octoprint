using Microsoft.Extensions.DependencyInjection;
using Overseer.Server.Integration;
using Overseer.Server.Integration.Machines;

namespace Overseer.OctoPrint;

public class OctoPrintPluginConfiguration : IPluginConfiguration
{
  public void ConfigureServices(IServiceCollection services)
  {
    services.AddTransient<IMachineConfigurationProvider<OctoPrintMachine>, OctoPrintMachineConfigurationProvider>();
    services.AddTransient<IMachineProvider<OctoPrintMachine>, OctoPrintMachineProvider>();
  }
}
