using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using log4net;
using Overseer.OctoPrint.Models;
using Overseer.Server.Integration.Machines;
using Timer = System.Timers.Timer;

namespace Overseer.OctoPrint;

public sealed class OctoPrintMachineProvider(OctoPrintMachine machine, IHttpClientFactory httpClientFactory)
  : IMachineProvider<OctoPrintMachine>,
    IDisposable
{
  static readonly ILog Log = LogManager.GetLogger(typeof(OctoPrintMachineProvider));

  public event EventHandler<MachineStatusEventArgs>? StatusUpdated;

  public string MachineType => "OctoPrint";

  const int MaxExceptionCount = 5;

  const int ExceptionTimeout = 2;

  readonly Stopwatch _stopwatch = new();

  Timer? _timer;

  HttpClient? _manualHttpClient;

  string? _lastClientCertificateThumbprint;

  X509Certificate2Collection? _clientCertificateChain;

  int _exceptionCount;

  public OctoPrintMachine Machine
  {
    get => machine;
    private set => machine = value;
  }

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

  public async Task Configure(Machine machine)
  {
    OctoPrintMachine updatedMachine = (OctoPrintMachine)machine;
    if (updatedMachine.ApiKey != Machine.ApiKey)
    {
      Machine.ApiKey = updatedMachine.ApiKey;
    }

    updatedMachine.Url = ProcessUri(updatedMachine.Url!, string.Empty).ToString();

    var settings = await Retrieve<Settings>("api/settings");
    if (!string.IsNullOrWhiteSpace(settings.WebCam?.StreamUrl))
    {
      updatedMachine.WebcamUrl = ProcessUri(updatedMachine.Url, settings.WebCam.StreamUrl).ToString();

      if (settings.WebCam.FlipH)
      {
        updatedMachine.WebcamOrientation = MachineWebcamOrientation.FlippedHorizontally;
      }
      if (settings.WebCam.FlipV)
      {
        updatedMachine.WebcamOrientation = MachineWebcamOrientation.FlippedVertically;
      }
    }

    var profiles = await Retrieve<PrinterProfiles>("api/printerprofiles");
    foreach (var profileProperty in profiles.Profiles ?? [])
    {
      if (profileProperty.Value == null)
        continue;

      var profile = profileProperty.Value;
      if (profile.Name == null)
        continue;
      if (profile.Id == null)
        continue;

      if (profile.Current)
      {
        var tools = new List<MachineTool>();
        if (profile.HeatedBed)
        {
          tools.Add(new MachineTool(MachineToolType.Heater, -1, "bed"));
        }

        if (profile.Extruder?.SharedNozzle == true)
        {
          tools.Add(new MachineTool(MachineToolType.Heater, 0));
        }

        for (int i = 0; i < profile.Extruder?.Count; i++)
        {
          if (!profile.Extruder.SharedNozzle)
          {
            tools.Add(new MachineTool(MachineToolType.Heater, i));
          }

          tools.Add(new MachineTool(MachineToolType.Extruder, i));
        }

        updatedMachine.Tools = tools;
      }
    }

    Machine = updatedMachine;
  }

  public void Start(int interval)
  {
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

  async Task Send(
    string resource,
    string method = "GET",
    object? body = null,
    Dictionary<string, string>? query = null,
    Dictionary<string, string>? headers = null
  )
  {
    using var response = await ExecuteRequestAsync(resource, method, body, query, headers);

    if (!response.IsSuccessStatusCode)
    {
      var content = await response.Content.ReadAsStringAsync();
      throw new Exception($"StatusCode: {(int)response.StatusCode}\nContent: {content}");
    }
  }

  async Task<T> Retrieve<T>(
    string resource,
    string method = "GET",
    object? body = null,
    Dictionary<string, string>? query = null,
    Dictionary<string, string>? headers = null
  )
  {
    using var response = await ExecuteRequestAsync(resource, method, body, query, headers);

    if (!response.IsSuccessStatusCode)
    {
      var content = await response.Content.ReadAsStringAsync();
      throw new Exception($"StatusCode: {(int)response.StatusCode}\nContent: {content}");
    }

    if (response.Content.Headers.ContentType?.MediaType?.Contains("json") != true)
    {
      return default!;
    }

    return (await response.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
  }

  async Task<HttpResponseMessage> ExecuteRequestAsync(
    string resource,
    string method,
    object? body,
    Dictionary<string, string>? query,
    Dictionary<string, string>? headers
  )
  {
    headers ??= [];
    headers.TryAdd("X-Api-Key", Machine.ApiKey!);

    var client = GetHttpClient();
    var uri = new Uri(new Uri(Machine.Url!), resource);

    if (query?.Count > 0)
    {
      var queryBuilder = new StringBuilder();
      var hasQuery = uri.Query.Length > 1;
      queryBuilder.Append(uri.Query);

      foreach (var kvp in query)
      {
        if (hasQuery)
          queryBuilder.Append('&');
        else
          queryBuilder.Append('?');
        hasQuery = true;

        queryBuilder.Append(Uri.EscapeDataString(kvp.Key));
        queryBuilder.Append('=');
        queryBuilder.Append(Uri.EscapeDataString(kvp.Value));
      }

      var uriBuilder = new UriBuilder(uri) { Query = queryBuilder.ToString() };
      uri = uriBuilder.Uri;
    }

    var httpMethod = new HttpMethod(method);
    using var request = new HttpRequestMessage(httpMethod, uri);

    if (body != null)
    {
      request.Content = JsonContent.Create(body);
    }

    foreach (var header in headers)
    {
      request.Headers.TryAddWithoutValidation(header.Key, header.Value);
    }

    return await client.SendAsync(request);
  }

  HttpClient GetHttpClient()
  {
    var currentThumbprint = Machine.ClientCertificate;

    if (!string.IsNullOrWhiteSpace(currentThumbprint))
    {
      if (_manualHttpClient != null && currentThumbprint == _lastClientCertificateThumbprint)
      {
        return _manualHttpClient;
      }

      _manualHttpClient?.Dispose();
      _lastClientCertificateThumbprint = currentThumbprint;

      var handler = new HttpClientHandler();
      var certs = GetClientCertificate(currentThumbprint);
      if (certs?.Count > 0)
      {
        handler.ClientCertificates.AddRange(certs);
      }

      _manualHttpClient = new HttpClient(handler);
      return _manualHttpClient;
    }

    if (_manualHttpClient != null)
    {
      _manualHttpClient.Dispose();
      _manualHttpClient = null;
      _lastClientCertificateThumbprint = null;
    }

    return httpClientFactory.CreateClient();
  }

  X509Certificate2Collection? GetClientCertificate(string? commonName)
  {
    if (string.IsNullOrWhiteSpace(commonName))
      return null;

    if (_clientCertificateChain?.Find(X509FindType.FindBySubjectName, commonName, false).Count > 0)
      return _clientCertificateChain;

    var certificateStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
    certificateStore.Open(OpenFlags.ReadOnly);

    _clientCertificateChain = certificateStore.Certificates.Find(X509FindType.FindBySubjectName, commonName, false);
    certificateStore.Close();

    return _clientCertificateChain;
  }

  static Uri ProcessUri(string url, string refPath = "")
  {
    refPath ??= string.Empty;
    var uri = new Uri(url);

    if (Uri.TryCreate(refPath, UriKind.Absolute, out var refUri) && refUri.Scheme.StartsWith("http"))
    {
      if (refUri.Host == "localhost" || IPAddress.TryParse(refUri.Host, out var ip) && IPAddress.IsLoopback(ip))
      {
        refPath = refUri.PathAndQuery;
      }
      else
      {
        return refUri;
      }
    }

    return new Uri(uri, refPath);
  }

  public void Dispose()
  {
    _timer?.Dispose();
    _manualHttpClient?.Dispose();
  }
}
