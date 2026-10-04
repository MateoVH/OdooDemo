using System.Xml;
using OdooConnector.Tests.Infrastructure;
using OdooConnector.XmlRpc;

namespace OdooConnector.Tests.XmlRpc;

public sealed class XmlRpcDeserializerTests
{
    [Fact]
    public void ParsesASearchReadResponseWrittenByPython()
    {
        var response = XmlRpcDeserializer.ParseMethodResponse(PythonFixtures.PartnersSearchRead);

        Assert.Null(response.Fault);
        var rows = Assert.IsType<object?[]>(response.Value);
        Assert.Equal(2, rows.Length);

        var azure = Assert.IsType<Dictionary<string, object?>>(rows[0]);
        Assert.Equal(14, azure["id"]);
        Assert.Equal("Azure Interior", azure["name"]);
        Assert.Equal(true, azure["is_company"]);
        Assert.Equal(false, azure["vat"]);
        Assert.Equal(new object?[] { 233, "United States" }, Assert.IsType<object?[]>(azure["country_id"]));

        var nandu = Assert.IsType<Dictionary<string, object?>>(rows[1]);
        Assert.Equal("Ñandú & Cía <S.A.S>", nandu["name"]);
        Assert.Equal("Medellín", nandu["city"]);
        Assert.Equal(false, nandu["email"]);
    }

    [Fact]
    public void ParsesEveryScalarTypeWrittenByPython()
    {
        var values = Assert.IsType<object?[]>(XmlRpcDeserializer.ParseMethodResponse(PythonFixtures.Scalars).Value);

        Assert.Equal(42, values[0]);
        Assert.Equal(-7, values[1]);
        Assert.Equal(true, values[2]);
        Assert.Equal(false, values[3]);
        Assert.Equal(1190.0, values[4]);
        Assert.Equal(99.99, values[5]);
        Assert.Equal("  padded text  ", values[6]);
        Assert.Equal(string.Empty, values[7]);
        Assert.Null(values[8]);
        Assert.Equal("%PDF-1.7 demo"u8.ToArray(), Assert.IsType<byte[]>(values[9]));
        Assert.Equal(new DateTime(2026, 10, 4, 13, 45, 0), values[10]);
        Assert.Empty(Assert.IsType<Dictionary<string, object?>>(values[11]));
        Assert.Empty(Assert.IsType<object?[]>(values[12]));
    }

    [Theory]
    [InlineData("<value>plain text</value>", "plain text")]
    [InlineData("<value>  keeps spaces  </value>", "  keeps spaces  ")]
    [InlineData("<value></value>", "")]
    [InlineData("<value><string/></value>", "")]
    public void TreatsUntypedValuesAsStrings(string value, string expected)
    {
        Assert.Equal(expected, ParseSingleValue(value));
    }

    [Fact]
    public void ParsesIntegersOfEveryWidth()
    {
        Assert.Equal(7, ParseSingleValue("<value><i4>7</i4></value>"));
        Assert.Equal(9_007_199_254_740_993L, ParseSingleValue("<value><i8>9007199254740993</i8></value>"));
    }

    [Theory]
    [InlineData("20261004T13:45:00")]
    [InlineData("2026-10-04T13:45:00")]
    [InlineData("2026-10-04T13:45:00Z")]
    [InlineData("2026-10-04T08:45:00-05:00")]
    public void ParsesIso8601DateTimes(string text)
    {
        var value = Assert.IsType<DateTime>(ParseSingleValue($"<value><dateTime.iso8601>{text}</dateTime.iso8601></value>"));

        Assert.Equal(new DateTime(2026, 10, 4, 13, 45, 0), value);
    }

    [Fact]
    public void ParsesAFaultWrittenByPython()
    {
        var response = XmlRpcDeserializer.ParseMethodResponse(PythonFixtures.UserErrorFault);

        Assert.Equal(new XmlRpcFault(2, "The field 'Customer' is required to post the invoice."), response.Fault);
    }

    [Fact]
    public void AcceptsTheStringFaultCodesOfTheLegacyEndpoint()
    {
        const string xml = """
            <methodResponse><fault><value><struct>
              <member><name>faultCode</name><value><string>warning -- Access Denied</string></value></member>
              <member><name>faultString</name><value><string>Access Denied</string></value></member>
            </struct></value></fault></methodResponse>
            """;

        Assert.Equal(new XmlRpcFault(0, "Access Denied"), XmlRpcDeserializer.ParseMethodResponse(xml).Fault);
    }

    [Fact]
    public void RejectsDocumentTypeDefinitions()
    {
        // A DTD is the entry point of XXE and "billion laughs" attacks; Odoo never sends one.
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE lolz [<!ENTITY lol "lol"><!ENTITY lol2 "&lol;&lol;&lol;&lol;&lol;">]>
            <methodResponse><params><param><value><string>&lol2;</string></value></param></params></methodResponse>
            """;

        Assert.Throws<XmlException>(() => XmlRpcDeserializer.ParseMethodResponse(xml));
    }

    [Theory]
    [InlineData("<html><body>Odoo login page</body></html>")]
    [InlineData("<methodResponse/>")]
    [InlineData("<methodResponse><params><param><value><decimal>1</decimal></value></param></params></methodResponse>")]
    [InlineData("<methodResponse><params><param><value><int>one</int></value></param></params></methodResponse>")]
    [InlineData("<methodResponse><params><param><value><boolean>yes</boolean></value></param></params></methodResponse>")]
    [InlineData("<methodResponse><params><param><value><struct><member><value>1</value></member></struct></value></param></params></methodResponse>")]
    public void RejectsDocumentsThatAreNotValidResponses(string xml)
    {
        Assert.Throws<FormatException>(() => XmlRpcDeserializer.ParseMethodResponse(xml));
    }

    [Fact]
    public void RejectsMalformedXml()
    {
        Assert.Throws<XmlException>(() => XmlRpcDeserializer.ParseMethodResponse("<methodResponse><params>"));
    }

    private static object? ParseSingleValue(string valueXml) =>
        XmlRpcDeserializer.ParseMethodResponse($"<methodResponse><params><param>{valueXml}</param></params></methodResponse>").Value;
}
