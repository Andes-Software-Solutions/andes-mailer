using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Andes.Mailer.Api.Filters.Exceptions;
using Andes.Mailer.Api.Idempotency;
using Andes.Mailer.Api.Idempotency.Constants;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Service.Exceptions;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.Identity.Web;

namespace Andes.Mailer.Api.Filters;

// Added first on an endpoint, so a replay returns the caller's earlier success before the permission filters run again: once a
// change such as leaving a team has applied, those filters would refuse the same request. A request that fails a later filter
// gets a non-2xx result or an exception, which frees its key.
internal sealed class IdempotencyEndpointFilter(IIdempotencyStore store) : IEndpointFilter
{
    private static readonly System.Reflection.Assembly _dtoAssembly = typeof(PaginatedResponseDto<>).Assembly;

    private readonly IIdempotencyStore _store = store;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var values = httpContext.Request.Headers[IdempotencyHeaders.Key];
        if (values.Count == 0)
        {
            return await next(context);
        }

        var key = ParseKey(values);

        // Scoped to the caller, so one person's key never replays another person's response.
        var callerId = httpContext.User.GetObjectId() ?? throw new ForbiddenException("The token does not identify a user.");
        var scopedKey = $"{callerId}:{key}";
        var fingerprint = Fingerprint(context);

        var lookup = _store.Begin(scopedKey, fingerprint);
        switch (lookup.Outcome)
        {
            case IdempotencyOutcomes.Completed:
                httpContext.Response.Headers[IdempotencyHeaders.Replayed] = "true";
                return lookup.Result;
            case IdempotencyOutcomes.InProgress:
                throw new IdempotencyRequestInProgressException();
            case IdempotencyOutcomes.Mismatch:
                throw new IdempotencyKeyMismatchException();
            case IdempotencyOutcomes.Started:
            default:
                break;
        }

        object? result;
        try
        {
            result = await next(context);
        }
        catch
        {
            _store.Release(scopedKey, lookup.Reservation);
            throw;
        }

        // Only successes are kept: after a failure the client may fix the request and retry with the same key.
        if (result is IStatusCodeHttpResult { StatusCode: >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices })
        {
            _store.Complete(scopedKey, lookup.Reservation, result);
        }
        else
        {
            _store.Release(scopedKey, lookup.Reservation);
        }

        return result;
    }

    #region Private methods

    private static string ParseKey(Microsoft.Extensions.Primitives.StringValues values)
    {
        // RFC 8941 sends the key as a quoted string; plain tokens are common too, so both are accepted.
        var key = values.Count == 1 ? values[0]?.Trim() ?? string.Empty : string.Empty;
        if (key.Length >= 2 && key[0] == '"' && key[^1] == '"')
        {
            key = key[1..^1];
        }

        if (key.Length is 0 or > IdempotencyHeaders.KeyMaxLength || !key.All(c => c is > ' ' and <= '~'))
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    IdempotencyHeaders.Key,
                    $"Send one {IdempotencyHeaders.Key} of 1 to {IdempotencyHeaders.KeyMaxLength} visible ASCII characters, such as a UUID."),
            ]);
        }

        return key;
    }

    private static string Fingerprint(EndpointFilterInvocationContext context)
    {
        var request = context.HttpContext.Request;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes($"{request.Method} {request.Path.Value?.ToUpperInvariant()}\n"));

        // The bound request DTOs stand in for the body, which model binding has already read.
        foreach (var argument in context.Arguments.OfType<object>().Where(a => a.GetType().Assembly == _dtoAssembly))
        {
            hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(argument, argument.GetType(), JsonSerializerOptions.Web));
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    #endregion
}
