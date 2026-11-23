using Andes.Mailer.Service.Security;
using Andes.Mailer.Service.Users;

namespace Andes.Mailer.Api.Configuration;

internal static class UsersConfiguration
{
    #region Public static methods

    public static IServiceCollection AddUsers(this IServiceCollection services)
    {
        // Holds directory search results, which are the same for every caller.
        services.AddHybridCache();
        services.AddScoped<IDirectoryUserService, DirectoryUserService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IPermissionService, PermissionService>();

        return services;
    }

    #endregion
}
