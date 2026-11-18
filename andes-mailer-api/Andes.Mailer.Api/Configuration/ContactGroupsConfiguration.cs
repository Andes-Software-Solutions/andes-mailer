using Andes.Mailer.Service.ContactGroups;

namespace Andes.Mailer.Api.Configuration;

internal static class ContactGroupsConfiguration
{
    #region Public static methods

    public static IServiceCollection AddContactGroups(this IServiceCollection services)
    {
        services.AddScoped<IContactGroupService, ContactGroupService>();

        return services;
    }

    #endregion
}
