namespace Overseer.OctoPrint.Models;

internal class State
{
  public string? Text { get; set; }
  public string? Error { get; set; }
  public Flags Flags { get; set; } = new();
}
