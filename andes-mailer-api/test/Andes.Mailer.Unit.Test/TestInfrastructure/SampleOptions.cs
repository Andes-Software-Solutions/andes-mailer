using FluentValidation;

namespace Andes.Mailer.Unit.Test.TestInfrastructure;

public sealed class SampleOptions
{
    public string Value { get; set; } = string.Empty;
}

public sealed class SampleOptionsValidator : AbstractValidator<SampleOptions>
{
    public SampleOptionsValidator() => RuleFor(o => o.Value).NotEmpty();
}
