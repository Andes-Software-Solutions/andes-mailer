using Andes.Mailer.Service.Security;
using Andes.Mailer.Service.Users;

namespace Andes.Mailer.Api.Configuration;

internal static class UsersConfiguration
{
    #region Public static methods

    public static IServiceCollection AddUsers(this IServiceCollection services)
    {
        services.AddScoped<IDirectoryUserService, DirectoryUserService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPermissionService, PermissionService>();

        return services;
    }

    #endregion
}
