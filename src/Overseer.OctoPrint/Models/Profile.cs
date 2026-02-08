namespace Overseer.OctoPrint.Models;

internal class Profile
{
  public string? Id { get; set; }
  public string? Name { get; set; }
  public string? Color { get; set; }
  public string? Model { get; set; }
  public bool Default { get; set; }
  public bool Current { get; set; }
  public string? Resource { get; set; }
  public Volume? Volume { get; set; }
  public bool HeatedBed { get; set; }
  public bool HeatedChamber { get; set; }
  public Dictionary<string, Axis>? Axes { get; set; }
  public Extruder? Extruder { get; set; }
}
