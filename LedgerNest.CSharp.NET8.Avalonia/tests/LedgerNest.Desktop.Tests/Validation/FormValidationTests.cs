using LedgerNest.Desktop;
using System.Globalization;

namespace LedgerNest.Desktop.Tests.Validation;

[Trait("Category", "Unit")]
public sealed class FormValidationTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("\t", false)]
    [InlineData("value", true)]
    public void Validate_RequiredText_RejectsBlank(string value, bool expected)
    {
        var field = new FormField("Name", value, required: true);
        Assert.Equal(expected, field.Validate());
        Assert.Equal(expected, field.Error.Length == 0);
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("0.01", true)]
    [InlineData("79228162514264337593543950335", true)]
    [InlineData("79228162514264337593543950336", false)]
    [InlineData("-0.01", false)]
    [InlineData("NaN", false)]
    [InlineData("Infinity", false)]
    [InlineData("oops", false)]
    public void Validate_NumericBoundary_RejectsNegativeMalformedOrOverflow(string value, bool expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var field = new FormField("Amount", value, "number", required: true);
            Assert.Equal(expected, field.Validate());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("person@example.test", true)]
    [InlineData("invalid", false)]
    [InlineData("a@", false)]
    public void Validate_Email_RejectsMalformedOptionalValues(string value, bool expected)
        => Assert.Equal(expected, new FormField("Email", value).Validate());

    [Fact]
    public void Validate_CorrectedInput_ClearsPreviousError()
    {
        var field = new FormField("Name", required: true);
        Assert.False(field.Validate());
        field.Value = "Valid";
        Assert.True(field.Validate());
        Assert.Empty(field.Error);
    }
}
