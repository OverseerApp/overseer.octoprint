namespace Overseer.OctoPrint.Models;

internal class JobDetails
{
  public FileDetails? File { get; set; }
  public double? EstimatedPrintTime { get; set; }
  public double? AveragePrintTime { get; set; }
  public Dictionary<string, Filament>? Filament { get; set; }
}
