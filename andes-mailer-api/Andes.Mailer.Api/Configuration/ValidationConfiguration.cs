using Andes.Mailer.Dto.Pagination;

using FluentValidation;

namespace Andes.Mailer.Api.Configuration;

internal static class ValidationConfiguration
{
    #region Public static methods

    public static IServiceCollection AddAndesMailerValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining(typeof(PaginatedResponseDto<>), includeInternalTypes: true);

        return services;
    }

    #endregion
}
