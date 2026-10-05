using LedgerNest.Desktop;
using LedgerNest.Desktop.Tests.Fixtures;

namespace LedgerNest.Desktop.Tests.Validation;

public sealed class ValidationFailureTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_TextExceedsDeclaredMaximum_RejectedAtModelBoundary()
    {
        var field = new FormField("Name", new string('x', 51), required: true) { MaxLength = 50 };
        Assert.False(field.Validate());
        Assert.NotEmpty(field.Error);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Validate_NullNumericInput_RejectedWithoutCrash()
    {
        var field = new FormField("Amount", null!, "number", required: true);
        var exception = Record.Exception(() => Assert.False(field.Validate()));
        Assert.Null(exception);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void CreateUser_PasswordBelowMinimum_RejectedWithoutPartialRecord()
    {
        using var fixture = new TestDatabaseFixture();
        var model = fixture.CreateModel();
        Assert.False(model.SaveRecord("User", TestData.UserFields(model, "short-password", password: "short")));
        Assert.Single(fixture.CreateModel().Users);
    }
}
