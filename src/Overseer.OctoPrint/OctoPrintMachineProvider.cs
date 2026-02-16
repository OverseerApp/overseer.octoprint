using System.Diagnostics;
using log4net;
using Overseer.OctoPrint.Models;
using Overseer.Server.Integration.Machines;
using Timer = System.Timers.Timer;

namespace Overseer.OctoPrint;

public sealed class OctoPrintMachineProvider(IHttpClientFactory httpClientFactory)
  : OctoPrintApiClient(httpClientFactory),
    IMachineProvider<OctoPrintMachine>
{
  static readonly ILog Log = LogManager.GetLogger(typeof(OctoPrintMachineProvider));

  public event EventHandler<MachineStatusEventArgs>? StatusUpdated;

  const int MaxExceptionCount = 5;

  const int ExceptionTimeout = 2;

  readonly Stopwatch _stopwatch = new();

  readonly SemaphoreSlim _pollSemaphore = new(1, 1);

  Timer? _timer;

  int _exceptionCount;

  public Task PauseJob()
  {
    return Send("api/job", method: "POST", body: new { command = "pause", action = "pause" });
  }

  public Task ResumeJob()
  {
    return Send("api/job", method: "POST", body: new { command = "pause", action = "resume" });
  }

  public Task CancelJob()
  {
    return Send("api/job", method: "POST", body: new { command = "cancel" });
  }

  public void Start<TMachine>(int interval, TMachine machine)
    where TMachine : Machine, new()
  {
    Machine = machine as OctoPrintMachine ?? new OctoPrintMachine(machine);

    _timer?.Dispose();
    _timer = new(interval);
    _timer.Elapsed += async (sender, args) => await Poll();
    _timer.Start();

    Task.Run(Poll);
  }

  public void Stop()
  {
    _timer?.Dispose();
    _timer = null;
  }

  async Task Poll()
  {
    if (!await _pollSemaphore.WaitAsync(0))
      return;

    if (Machine is null)
      return;

    try
    {
      if (_stopwatch.IsRunning && _stopwatch.Elapsed.TotalMinutes < ExceptionTimeout)
      {
        StatusUpdated?.Invoke(this, new MachineStatusEventArgs(new() { MachineId = Machine.Id }));
        return;
      }

      try
      {
        var printerStatus = await Retrieve<Status>("api/printer");
        var status = new MachineStatus { MachineId = Machine.Id };
        Machine
          .Tools.Where(t => t.ToolType == MachineToolType.Heater)
          .ToList()
          .ForEach(t =>
          {
            var key = t.Index == -1 ? "bed" : $"tool{t.Index}";
            if (printerStatus.Temperature?.TryGetValue(key, out var temp) == true)
            {
              status.Temperatures.Add(
                t.Index,
                new()
                {
                  HeaterIndex = t.Index,
                  Actual = temp.Actual ?? 0,
                  Target = temp.Target ?? 0,
                }
              );
            }
          });

        status.State = printerStatus.State?.Flags switch
        {
          { Paused: true } or { Pausing: true } => MachineState.Paused,
          { Printing: true } or { Resuming: true } => MachineState.Operational,
          _ => MachineState.Idle,
        };

        if (status.State == MachineState.Operational || status.State == MachineState.Paused)
        {
          var jobStatus = await Retrieve<Job>("api/job");
          status.ElapsedJobTime = jobStatus.Progress?.PrintTime ?? 0;
          status.EstimatedTimeRemaining = jobStatus.Progress?.PrintTimeLeft ?? 0;
          status.Progress = Math.Round(jobStatus.Progress?.Completion ?? 0, 1);
        }

        _exceptionCount = 0;
        _stopwatch.Stop();

        StatusUpdated?.Invoke(this, new MachineStatusEventArgs(status));
      }
      catch (Exception ex)
      {
        if (++_exceptionCount >= MaxExceptionCount)
        {
          _stopwatch.Restart();
          Log.Error("Max consecutive failure count reached, throttling updates", ex);
        }

        StatusUpdated?.Invoke(this, new MachineStatusEventArgs(new() { MachineId = Machine.Id }));
      }
    }
    finally
    {
      _pollSemaphore.Release();
    }
  }

  public override void Dispose()
  {
    _timer?.Dispose();
    _pollSemaphore.Dispose();
    base.Dispose();
  }
}
