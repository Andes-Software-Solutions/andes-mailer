using Andes.Mailer.Entity.Users;

namespace Andes.Mailer.Unit.Test.Entity.Users;

public sealed class UserTests
{
    [Fact]
    public void Email_MixedCase_StoresLowercase()
    {
        var user = new User { FirstName = "Ada", LastName = "Lovelace", Email = "Ada.Lovelace@Contoso.COM" };

        Assert.Equal("ada.lovelace@contoso.com", user.Email);
    }

    [Theory]
    [InlineData("Ada", "Lovelace", "Ada Lovelace")]
    [InlineData("Ada", "", "Ada")]
    public void FullName_Names_JoinsWithoutStraySpace(string firstName, string lastName, string expected)
    {
        var user = new User { FirstName = firstName, LastName = lastName, Email = "ada@contoso.com" };

        Assert.Equal(expected, user.FullName);
    }
}
