using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace Overseer.OctoPrint;

/// <summary>
/// Provides HTTP communication functionality for interacting with the OctoPrint API.
/// Handles authentication, certificate management, and request/response processing.
/// </summary>
public abstract class OctoPrintApiClient : IDisposable
{
  readonly IHttpClientFactory _httpClientFactory;

  HttpClient? _manualHttpClient;

  string? _lastClientCertificateThumbprint;

  X509Certificate2Collection? _clientCertificateChain;

  protected OctoPrintMachine? Machine { get; set; }

  protected OctoPrintApiClient(IHttpClientFactory httpClientFactory)
  {
    _httpClientFactory = httpClientFactory;
  }

  /// <summary>
  /// Sends an HTTP request to the OctoPrint API without expecting a response body.
  /// </summary>
  protected async Task Send(
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
      throw new HttpRequestException($"Request failed with status code {(int)response.StatusCode}. Content: {content}");
    }
  }

  /// <summary>
  /// Sends an HTTP request to the OctoPrint API and deserializes the response to the specified type.
  /// </summary>
  protected async Task<T> Retrieve<T>(
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
      throw new HttpRequestException($"Request failed with status code {(int)response.StatusCode}. Content: {content}");
    }

    // Handle successful responses with no content explicitly (e.g., HTTP 204).
    if (response.StatusCode == HttpStatusCode.NoContent)
    {
      return default!;
    }

    // For other successful responses, we expect JSON; treat unexpected content types as errors.
    if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true)
    {
      var contentType = response.Content.Headers.ContentType?.ToString() ?? "<unknown>";
      throw new InvalidOperationException($"Unexpected content type: {contentType}. Expected JSON response.");
    }

    return (await response.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
  }

  /// <summary>
  /// Executes an HTTP request to the OctoPrint API with the specified parameters.
  /// </summary>
  async Task<HttpResponseMessage> ExecuteRequestAsync(
    string resource,
    string method,
    object? body,
    Dictionary<string, string>? query,
    Dictionary<string, string>? headers
  )
  {
    if (Machine == null)
    {
      throw new InvalidOperationException("Machine configuration is not set.");
    }

    headers ??= [];
    headers.TryAdd("X-Api-Key", Machine.ApiKey!);

    var client = GetHttpClient();
    var uri = BuildUri(resource, query);

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

  /// <summary>
  /// Builds a complete URI from the base machine URL, resource path, and optional query parameters.
  /// </summary>
  Uri BuildUri(string resource, Dictionary<string, string>? query)
  {
    var uri = new Uri(new Uri(Machine!.Url!), resource);

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

    return uri;
  }

  /// <summary>
  /// Gets an HttpClient configured with the appropriate certificate if necessary.
  /// </summary>
  HttpClient GetHttpClient()
  {
    var currentThumbprint = Machine?.ClientCertificate;

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

    return _httpClientFactory.CreateClient();
  }

  /// <summary>
  /// Retrieves a client certificate from the certificate store by thumbprint.
  /// </summary>
  X509Certificate2Collection? GetClientCertificate(string? thumbprint)
  {
    if (string.IsNullOrWhiteSpace(thumbprint))
      return null;

    // Normalize thumbprint by removing spaces that are often present when copied from UI tools
    var normalizedThumbprint = thumbprint.Replace(" ", string.Empty);

    if (_clientCertificateChain?.Find(X509FindType.FindByThumbprint, normalizedThumbprint, false).Count > 0)
      return _clientCertificateChain;

    var certificateStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
    certificateStore.Open(OpenFlags.ReadOnly);

    _clientCertificateChain = certificateStore.Certificates.Find(X509FindType.FindByThumbprint, normalizedThumbprint, false);
    certificateStore.Close();

    return _clientCertificateChain;
  }

  public virtual void Dispose()
  {
    _manualHttpClient?.Dispose();
  }
}
