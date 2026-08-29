namespace Chronaiq.Application.Common.Exceptions;

/// <summary>
/// Thrown by handlers when a referenced aggregate does not exist. Translated to an
/// HTTP 404 by the API's exception-handling layer.
/// </summary>
public sealed class NotFoundException(string entity, object key)
    : Exception($"{entity} with key '{key}' was not found.")
{
    public string Entity { get; } = entity;
    public object Key { get; } = key;
}
