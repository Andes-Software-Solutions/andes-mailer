using Andes.Mailer.Common.Constants.Users;
using Andes.Mailer.Dto.Actions.Users;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Users;

public sealed class SearchDirectoryActionDtoValidatorTests
{
    private readonly SearchDirectoryActionDtoValidator _validator = new();

    [Theory]
    [InlineData("an")]
    [InlineData("Ana Rojas")]
    [InlineData("José Núñez")]
    [InlineData("o'brien")]
    [InlineData("ana.rojas+news@contoso-mail.com")]
    [InlineData("first_last")]
    public void Validate_Query_IsValid(string query) =>
        Assert.True(_validator.Validate(new SearchDirectoryActionDto(query)).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("  a  ")]
    [InlineData("ana & co")]
    [InlineData("ana\"")]
    [InlineData("ana*")]
    [InlineData("ana\tco")]
    public void Validate_InvalidQuery_FailsOnQuery(string query)
    {
        var result = _validator.Validate(new SearchDirectoryActionDto(query));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchDirectoryActionDto.Query));
    }

    [Fact]
    public void Validate_QueryOverMaxLength_FailsOnQuery()
    {
        var result = _validator.Validate(new SearchDirectoryActionDto(new string('a', DirectorySearchLimits.QueryMaxLength + 1)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchDirectoryActionDto.Query));
    }

    [Fact]
    public void Validate_MaxLengthQueryWithSurroundingSpaces_IsValid() =>
        Assert.True(_validator.Validate(new SearchDirectoryActionDto($"  {new string('a', DirectorySearchLimits.QueryMaxLength)}  ")).IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(DirectorySearchLimits.MaxLimit + 1)]
    public void Validate_LimitOutOfRange_FailsOnLimit(int limit)
    {
        var result = _validator.Validate(new SearchDirectoryActionDto("ana", limit));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SearchDirectoryActionDto.Limit));
    }
}
