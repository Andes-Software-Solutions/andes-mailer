using Andes.Mailer.Service.Teams;

namespace Andes.Mailer.Api.Configuration;

internal static class TeamsConfiguration
{
    #region Public static methods

    public static IServiceCollection AddTeams(this IServiceCollection services)
    {
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<ITeamMemberService, TeamMemberService>();

        return services;
    }

    #endregion
}
