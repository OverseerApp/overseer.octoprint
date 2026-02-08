using System.Text.Json.Serialization;

namespace Overseer.OctoPrint.Models;

public abstract class Machine
{
  public int Id { get; set; }

  public string? Name { get; set; }

  public bool Disabled { get; set; }

  public string? WebCamUrl { get; set; }

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public WebCamOrientation WebCamOrientation { get; set; }

  public IEnumerable<MachineTool> Tools { get; set; } = new List<MachineTool>();

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public abstract MachineType MachineType { get; }

  public int SortIndex { get; set; }
}
