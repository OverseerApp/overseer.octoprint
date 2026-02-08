namespace Overseer.OctoPrint.Models;

internal class Temperature
{
  public double? Actual { get; set; }
  public double? Target { get; set; }
  public double Offset { get; set; }
}
