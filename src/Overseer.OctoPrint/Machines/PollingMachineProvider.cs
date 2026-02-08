using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Overseer.OctoPrint.Channels;
using Overseer.OctoPrint.Models;
using RestSharp;
using Timer = System.Timers.Timer;

namespace Overseer.OctoPrint.Machines;

public abstract class PollingMachineProvider<TMachine> : MachineProvider<TMachine>
  where TMachine : Machine, IPollingMachine, new()
{
  public class FetchRequest
  {
    public string Method { get; set; } = "GET";
    public Dictionary<string, string> Query { get; set; } = [];
    public Dictionary<string, string> Headers { get; set; } = [];
    public object? Body { get; set; } = null;
  }

  protected abstract IMachineStatusChannel StatusChannel { get; }

  const int MaxExceptionCount = 5;

  const int ExceptionTimeout = 2;

  readonly Stopwatch _stopwatch = new();

  Timer? _timer;

  CancellationTokenSource? _cancellation;

  X509Certificate2Collection? _clientCertificateChain;

  int _exceptionCount;

  public override void Start(int interval)
  {
    _timer?.Dispose();
    _timer = new(interval);
    _timer.Elapsed += async (sender, args) => await Poll();
    _timer.Start();
    Task.Run(Poll);
  }

  public override void Stop()
  {
    _timer?.Dispose();
    _timer = null;
  }

  protected abstract Task<MachineStatus> AcquireStatus(CancellationToken cancellationToken);

  protected async Task Poll()
  {
    if (_stopwatch.IsRunning && _stopwatch.Elapsed.TotalMinutes < ExceptionTimeout)
    {
      await StatusChannel.WriteAsync(new() { MachineId = MachineId }, _cancellation?.Token ?? default);
    }

    try
    {
      if (_cancellation != null)
      {
        _cancellation.Cancel();
        _cancellation.Dispose();
      }

      _cancellation = new CancellationTokenSource();
      var status = await AcquireStatus(_cancellation.Token);

      _exceptionCount = 0;
      _stopwatch.Stop();
      _cancellation.Dispose();
      _cancellation = null;

      await StatusChannel.WriteAsync(status, _cancellation?.Token ?? default);
    }
    catch (Exception ex)
    {
      if (++_exceptionCount >= MaxExceptionCount)
      {
        _stopwatch.Restart();
        Log.Error("Max consecutive failure count reached, throttling updates", ex);
      }

      await StatusChannel.WriteAsync(new() { MachineId = MachineId }, _cancellation?.Token ?? default);
    }
  }

  protected virtual async Task Fetch(string resource, FetchRequest? fetchRequest = null, CancellationToken cancellation = default)
  {
    using var client = new RestClient(
      new RestClientOptions { BaseUrl = new Uri(Machine.Url!), ClientCertificates = GetClientCertificate(Machine.ClientCertificate) }
    );

    var request = new RestRequest(resource, (Method)Enum.Parse(typeof(Method), fetchRequest?.Method ?? "Get", true));
    if (fetchRequest?.Body != null)
    {
      request.AddJsonBody(fetchRequest.Body);
    }

    fetchRequest?.Headers?.ToList().ForEach(header => request.AddParameter(header.Key, header.Value, ParameterType.HttpHeader));
    fetchRequest?.Query?.ToList().ForEach(p => request.AddParameter(p.Key, p.Value, ParameterType.QueryString));

    var response = await client.ExecuteAsync(request, cancellation);

    if (response.ErrorException != null)
      throw response.ErrorException;

    if ((int)response.StatusCode >= 400)
    {
      throw new Exception($"StatusCode: {(int)response.StatusCode}\nContent: {response.Content}");
    }
  }

  protected virtual async Task<T> Fetch<T>(string resource, FetchRequest? fetchRequest = null, CancellationToken cancellation = default)
  {
    using var client = new RestClient(
      new RestClientOptions { BaseUrl = new Uri(Machine.Url!), ClientCertificates = GetClientCertificate(Machine.ClientCertificate) }
    );

    var request = new RestRequest(resource, (Method)Enum.Parse(typeof(Method), fetchRequest?.Method ?? "Get", true));
    if (fetchRequest?.Body != null)
    {
      request.AddJsonBody(fetchRequest.Body);
    }

    fetchRequest?.Headers?.ToList().ForEach(header => request.AddParameter(header.Key, header.Value, ParameterType.HttpHeader));
    fetchRequest?.Query?.ToList().ForEach(p => request.AddParameter(p.Key, p.Value, ParameterType.QueryString));

    var response = await client.ExecuteAsync<T>(request, cancellation);

    if (response.ErrorException != null)
      throw response.ErrorException;

    if ((int)response.StatusCode >= 400)
    {
      throw new Exception($"StatusCode: {(int)response.StatusCode}\nContent: {response.Content}");
    }

    if (string.IsNullOrWhiteSpace(response.Content))
      return default!;
    if (response.ContentType?.Contains("json") != true)
      return default!;

    return response.Data!;
  }

  X509Certificate2Collection? GetClientCertificate(string? commonName)
  {
    if (string.IsNullOrWhiteSpace(commonName))
      return null;

    if (_clientCertificateChain?.Find(X509FindType.FindBySubjectName, commonName, false).Count > 0)
      return _clientCertificateChain;

    try
    {
      var certificateStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
      certificateStore.Open(OpenFlags.ReadOnly);

      _clientCertificateChain = certificateStore.Certificates.Find(X509FindType.FindBySubjectName, commonName, false);
      certificateStore.Close();

      return _clientCertificateChain;
    }
    catch (Exception ex)
    {
      Log.Error($"Unable to find {commonName} for {StoreName.My} in {StoreLocation.CurrentUser}", ex);
      return null;
    }
  }

  protected static Uri ProcessUri(string url, string refPath = "")
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

  protected override void OnDisposing()
  {
    _timer?.Dispose();
    _cancellation?.Dispose();
  }
}
