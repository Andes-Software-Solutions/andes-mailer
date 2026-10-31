using FluentValidation;

namespace Andes.Mailer.Api.Filters;

internal sealed class ValidationEndpointFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
{
    private const string MissingRequestMessage = "A request is required.";

    private readonly IValidator<TRequest> _validator = validator;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Fails closed: an endpoint that asked for validation never runs on a request nobody validated.
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [typeof(TRequest).Name] = [MissingRequestMessage],
            });
        }

        var result = await _validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (!result.IsValid)
        {
            return TypedResults.ValidationProblem(result.ToDictionary());
        }

        return await next(context);
    }
}
