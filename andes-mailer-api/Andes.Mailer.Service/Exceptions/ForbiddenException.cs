namespace Andes.Mailer.Service.Exceptions;

/// <summary>
/// Signals that the caller may not access a resource. Maps to a 403 Problem Details response.
/// </summary>
/// <param name="message">A message for the caller; it reaches the client as the problem detail.</param>
public class ForbiddenException(string message) : Exception(message);
