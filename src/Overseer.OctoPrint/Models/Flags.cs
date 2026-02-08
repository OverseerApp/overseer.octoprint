namespace Overseer.OctoPrint.Models;

internal class Flags
{
  public bool Operational { get; set; }
  public bool Paused { get; set; }
  public bool Printing { get; set; }
  public bool Cancelling { get; set; }
  public bool Pausing { get; set; }
  public bool SdReady { get; set; }
  public bool Error { get; set; }
  public bool Ready { get; set; }
  public bool ClosedOnError { get; set; }
  public bool Resuming { get; set; }
}
