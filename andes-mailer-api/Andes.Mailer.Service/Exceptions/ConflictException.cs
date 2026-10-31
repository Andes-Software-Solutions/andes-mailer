namespace Andes.Mailer.Service.Exceptions;

/// <summary>
/// Signals that a request conflicts with the current state of a resource, such as a row-version mismatch. Maps to a 409 Problem Details response.
/// </summary>
/// <param name="message">A message for the caller; it reaches the client as the problem detail.</param>
public class ConflictException(string message) : Exception(message);
