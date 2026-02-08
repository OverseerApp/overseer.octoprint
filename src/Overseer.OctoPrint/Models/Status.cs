namespace Overseer.OctoPrint.Models;

internal class Status
{
  public Dictionary<string, Temperature>? Temperature { get; set; }

  public State? State { get; set; }
}
