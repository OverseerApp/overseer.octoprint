namespace Overseer.OctoPrint.Models;

internal class Progress
{
  public double? Completion { get; set; }
  public int? Filepos { get; set; }
  public int? PrintTime { get; set; }
  public int? PrintTimeLeft { get; set; }
}
