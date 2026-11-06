namespace Andes.Mailer.Repository.Sql.Constants;

internal static class SqlSchemas
{
    public const string Core = "Core";

    // One schema whose name contains a dot; EF Core quotes it as [Core.Ref].
    public const string CoreRef = "Core.Ref";
}
