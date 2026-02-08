namespace Overseer.OctoPrint.Models;

public class OverseerException(string key, object? properties = null) : Exception(key)
{
  public object? Properties { get; set; } = properties;

  public static void Unwrap(Exception ex)
  {
    if (ex is OverseerException)
      throw ex;

    var innerEx = ex.InnerException;
    while (innerEx != null)
    {
      if (innerEx is OverseerException)
        throw innerEx;
      innerEx = innerEx.InnerException;
    }
  }
}
