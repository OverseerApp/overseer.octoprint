using System.Text.Json.Serialization;

namespace Overseer.OctoPrint.Models;

internal class Job
{
  [JsonPropertyName("job")]
  public JobDetails? JobDetails { get; set; }
  public Progress? Progress { get; set; }
  public string? State { get; set; }
}
