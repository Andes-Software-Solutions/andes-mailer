using FluentValidation;

namespace Andes.Mailer.Repository.Sql.Options;

/// <summary>
/// Holds the settings of the SQL Server store.
/// </summary>
public sealed class SqlDbOptions
{
    /// <summary>
    /// The configuration section these options bind to.
    /// </summary>
    public const string SectionName = "SqlDb";

    /// <summary>
    /// Gets the connection string.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;
}

internal sealed class SqlDbOptionsValidator : AbstractValidator<SqlDbOptions>
{
    public SqlDbOptionsValidator() => RuleFor(o => o.ConnectionString).NotEmpty();
}
