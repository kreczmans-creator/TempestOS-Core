namespace Tempest.Core.Api;

/// <summary>
/// Thrown by an <see cref="ApiActionDelegate"/>'s own body-to-command
/// binding when the inbound request body cannot be bound to the action's
/// command — missing, malformed, or carrying values the command's own
/// constructor rejects. <see cref="ApiQueryRequestHandler"/> maps this
/// (and <see cref="System.Text.Json.JsonException"/>) to a
/// <c>400 Bad Request</c> carrying <see cref="Exception.Message"/>, never
/// a <c>500</c> — a caller-correctable input fault, not a platform fault.
/// </summary>
public sealed class ApiRequestBindingException : ApiException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ApiRequestBindingException"/> class.
    /// </summary>
    /// <param name="message">A message describing what the request body is missing or malformed about.</param>
    public ApiRequestBindingException(string message)
        : base(message)
    {
    }
}
