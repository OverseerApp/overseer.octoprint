namespace Overseer.OctoPrint.Models;

public class Extruder
{
  public int Count { get; set; }
  public bool SharedNozzle { get; set; }
  public double NozzleDiameter { get; set; }
}
