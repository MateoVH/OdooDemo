namespace OdooConnector.Tests;

public sealed class OdooVersionInfoTests
{
    [Theory]
    [InlineData("17.0", 17)]
    [InlineData("18.0", 18)]
    [InlineData("saas~18.3", 18)]
    [InlineData("20.0", 20)]
    [InlineData("", null)]
    [InlineData("unknown", null)]
    public void ExtractsTheMajorVersion(string serie, int? expected)
    {
        Assert.Equal(expected, new OdooVersionInfo($"{serie}+e", serie, 1).MajorVersion);
    }
}
