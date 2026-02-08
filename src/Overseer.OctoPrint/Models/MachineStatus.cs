using System.Text.Json.Serialization;

namespace Overseer.OctoPrint.Models;

public enum MachineState
{
  Offline = 0,
  Idle = 1,
  Paused = 2,
  Operational = 3,
}

public class TemperatureStatus
{
  public int HeaterIndex { get; set; }

  public double Actual { get; set; }

  public double Target { get; set; }
}

public class MachineStatus
{
  public Guid Id { get; set; } = Guid.NewGuid();

  public int MachineId { get; set; }

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public MachineState State { get; set; }

  public int ElapsedJobTime { get; set; }

  public int EstimatedTimeRemaining { get; set; }

  public double Progress { get; set; }

  public Dictionary<int, TemperatureStatus> Temperatures { get; set; } = [];

  public override bool Equals(object? obj)
  {
    if (obj is not MachineStatus other)
      return false;

    return MachineId == other.MachineId
      && State == other.State
      && ElapsedJobTime == other.ElapsedJobTime
      && EstimatedTimeRemaining == other.EstimatedTimeRemaining
      && Progress.Equals(other.Progress)
      && Temperatures.SequenceEqual(other.Temperatures);
  }

  public override int GetHashCode()
  {
    unchecked
    {
      int hash = 17;
      hash = hash * 23 + MachineId.GetHashCode();
      hash = hash * 23 + State.GetHashCode();
      hash = hash * 23 + ElapsedJobTime.GetHashCode();
      hash = hash * 23 + EstimatedTimeRemaining.GetHashCode();
      hash = hash * 23 + Progress.GetHashCode();
      foreach (var kvp in Temperatures)
      {
        hash = hash * 23 + kvp.Key.GetHashCode();
        hash = hash * 23 + (kvp.Value?.GetHashCode() ?? 0);
      }
      return hash;
    }
  }
}
