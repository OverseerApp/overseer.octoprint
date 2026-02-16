using System.Net;
using Overseer.OctoPrint.Models;
using Overseer.Server.Integration.Machines;

namespace Overseer.OctoPrint;

public class OctoPrintMachineConfigurationProvider(IHttpClientFactory httpClientFactory)
  : OctoPrintApiClient(httpClientFactory),
    IMachineConfigurationProvider<OctoPrintMachine>
{
  public async Task<OctoPrintMachine> Configure(Machine machine)
  {
    // Create a new OctoPrintMachine instance and copy properties from the base Machine
    // Depending if this is a creation or an update, the type may or may not be an OctoPrintMachine, so just recreate it.
    var updatedMachine = new OctoPrintMachine(machine);

    // Update Machine reference for API calls
    Machine = updatedMachine;

    updatedMachine.Url = ProcessUri(updatedMachine.Url!, string.Empty).ToString();

    var settings = await Retrieve<Settings>("api/settings");
    if (!string.IsNullOrWhiteSpace(settings.WebCam?.StreamUrl))
    {
      updatedMachine.WebcamUrl = ProcessUri(updatedMachine.Url, settings.WebCam.StreamUrl).ToString();

      if (settings.WebCam.FlipH)
      {
        updatedMachine.WebcamOrientation = MachineWebcamOrientation.FlippedHorizontally;
      }
      if (settings.WebCam.FlipV)
      {
        updatedMachine.WebcamOrientation = MachineWebcamOrientation.FlippedVertically;
      }
    }

    var profiles = await Retrieve<PrinterProfiles>("api/printerprofiles");
    foreach (var profileProperty in profiles.Profiles ?? [])
    {
      if (profileProperty.Value == null)
        continue;

      var profile = profileProperty.Value;
      if (profile.Name == null)
        continue;
      if (profile.Id == null)
        continue;

      if (profile.Current)
      {
        var tools = new List<MachineTool>();
        if (profile.HeatedBed)
        {
          tools.Add(new MachineTool(MachineToolType.Heater, -1, "bed"));
        }

        if (profile.Extruder?.SharedNozzle == true)
        {
          tools.Add(new MachineTool(MachineToolType.Heater, 0));
        }

        var extruderCount = profile.Extruder?.Count ?? 0;
        for (int i = 0; i < extruderCount; i++)
        {
          if (profile.Extruder?.SharedNozzle == false)
          {
            tools.Add(new MachineTool(MachineToolType.Heater, i));
          }

          tools.Add(new MachineTool(MachineToolType.Extruder, i));
        }

        updatedMachine.Tools = tools;
      }
    }

    return updatedMachine;
  }

  /// <summary>
  /// Processes a URI by resolving relative paths or handling localhost references.
  /// </summary>
  static Uri ProcessUri(string url, string refPath = "")
  {
    refPath ??= string.Empty;
    var uri = new Uri(url);

    if (Uri.TryCreate(refPath, UriKind.Absolute, out var refUri) && refUri.Scheme.StartsWith("http"))
    {
      if (refUri.Host == "localhost" || IPAddress.TryParse(refUri.Host, out var ip) && IPAddress.IsLoopback(ip))
      {
        refPath = refUri.PathAndQuery;
      }
      else
      {
        return refUri;
      }
    }

    return new Uri(uri, refPath);
  }
}
