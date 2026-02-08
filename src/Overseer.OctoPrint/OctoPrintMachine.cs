using Overseer.Server.Integration.Machines;

namespace Overseer.OctoPrint;

public class OctoPrintMachine : Machine
{
  public override string MachineType => "OctoPrint";

  [MachineProperty(
    displayName: "Webcam URL",
    description: "The URL of the webcam stream from the OctoPrint server.",
    displayType: MachinePropertyDisplayType.UpdateOnly
  )]
  public override string? WebcamUrl
  {
    get => base.WebcamUrl;
    set => base.WebcamUrl = value;
  }

  [MachineProperty(
    displayName: "Webcam Orientation",
    description: "The orientation of the webcam stream (e.g., normal, rotated 90 degrees, etc.).",
    displayType: MachinePropertyDisplayType.UpdateOnly
  )]
  public override MachineWebcamOrientation? WebcamOrientation
  {
    get => base.WebcamOrientation;
    set => base.WebcamOrientation = value;
  }

  [MachineProperty(
    displayName: "URL",
    description: "The URL of the OctoPrint server (e.g., http://octoprint.local).",
    isRequired: true,
    displayType: MachinePropertyDisplayType.SetupOnly
  )]
  public string? Url
  {
    get => GetProperty<string?>(nameof(Url));
    set => SetProperty(nameof(Url), value ?? string.Empty);
  }

  [MachineProperty(displayName: "API Key", description: "The API key for accessing the OctoPrint API.", isSensitive: true, isRequired: true)]
  public string? ApiKey
  {
    get => GetProperty<string?>(nameof(ApiKey));
    set => SetProperty(nameof(ApiKey), value ?? string.Empty);
  }

  [MachineProperty(
    displayName: "Client Certificate",
    description: @"
      Provide a thumbprint of the client certificate if your OctoPrint server requires client certificate authentication.
      This should be the thumbprint of the certificate locatable by the Certificate Store.
    "
  )]
  public string? ClientCertificate
  {
    get => GetProperty<string?>(nameof(ClientCertificate));
    set => SetProperty(nameof(ClientCertificate), value ?? string.Empty);
  }
}
