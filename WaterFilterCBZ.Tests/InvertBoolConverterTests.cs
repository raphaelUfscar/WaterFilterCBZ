using System.Globalization;

namespace WaterFilterCBZ.Tests;

public class InvertBoolConverterTests
{
    private readonly InvertBoolConverter _converter = new();

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Convert_InvertsBooleanValues(bool input, bool expected)
    {
        var result = _converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ConvertBack_InvertsBooleanValues(bool input, bool expected)
    {
        var result = _converter.ConvertBack(input, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_ReturnsNonBooleanValuesUnchanged()
    {
        const string input = "connected";

        var result = _converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Same(input, result);
    }

    [Fact]
    public void ConvertBack_ReturnsNonBooleanValuesUnchanged()
    {
        const string input = "connected";

        var result = _converter.ConvertBack(input, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Same(input, result);
    }
}
