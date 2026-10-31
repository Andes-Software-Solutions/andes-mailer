namespace Andes.Mailer.Service.Exceptions;

/// <summary>
/// Signals that a requested resource does not exist. Maps to a 404 Problem Details response.
/// </summary>
/// <param name="message">A message for the caller; it reaches the client as the problem detail.</param>
public class NotFoundException(string message) : Exception(message);
