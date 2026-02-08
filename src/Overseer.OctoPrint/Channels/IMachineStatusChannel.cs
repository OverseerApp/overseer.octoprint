using Overseer.OctoPrint.Models;

namespace Overseer.OctoPrint.Channels;

public interface IMachineStatusChannel
{
  Task WriteAsync(MachineStatus status, CancellationToken cancellationToken = default);
}
