using System.Globalization;
using System.Text;
using System.Xml.Linq;
using OdooConnector.Tests.Infrastructure;
using OdooConnector.XmlRpc;

namespace OdooConnector.Tests.XmlRpc;

public sealed class XmlRpcSerializerTests
{
    public static TheoryData<object?, string> Scalars => new()
    {
        { null, "<nil />" },
        { "text", "<string>text</string>" },
        { true, "<boolean>1</boolean>" },
        { false, "<boolean>0</boolean>" },
        { 42, "<int>42</int>" },
        { (short)-3, "<int>-3</int>" },
        { 7L, "<int>7</int>" },
        { 3_000_000_000L, "<i8>3000000000</i8>" },
        { 85.5, "<double>85.5</double>" },
        { 0.1f, "<double>0.1</double>" },
        { 19.99m, "<double>19.99</double>" },
        { new DateOnly(2026, 10, 4), "<string>2026-10-04</string>" },
        { new DateTime(2026, 10, 4, 13, 45, 0, DateTimeKind.Utc), "<string>2026-10-04 13:45:00</string>" },
        { new DateTimeOffset(2026, 10, 4, 8, 45, 0, TimeSpan.FromHours(-5)), "<string>2026-10-04 13:45:00</string>" },
        { new byte[] { 1, 2, 3 }, "<base64>AQID</base64>" },
    };

    [Fact]
    public void WritesTheMethodCallEnvelope()
    {
        var xml = Encoding.UTF8.GetString(XmlRpcSerializer.SerializeMethodCall("authenticate", ["demo", 2]));

        Assert.Equal(
            """<?xml version="1.0" encoding="utf-8"?><methodCall><methodName>authenticate</methodName><params><param><value><string>demo</string></value></param><param><value><int>2</int></value></param></params></methodCall>""",
            xml);
    }

    [Theory]
    [MemberData(nameof(Scalars))]
    public void WritesScalarValues(object? value, string expected)
    {
        Assert.Equal($"<value>{expected}</value>", ValueXml(value));
    }

    [Fact]
    public void EscapesXmlSpecialCharacters()
    {
        Assert.Equal("<value><string>Tom &amp; Jerry &lt;S.A.S&gt; \"quoted\"</string></value>", ValueXml("Tom & Jerry <S.A.S> \"quoted\""));
    }

    [Theory]
    [InlineData("Facturación electrónica – Año 2026 ✓")]
    [InlineData("Line 1\r\nLine 2\rLine 3\n")]
    [InlineData("  leading and trailing spaces  ")]
    public void PreservesTextExactly(string text)
    {
        Assert.Equal(text, RoundTrip(text));
    }

    [Fact]
    public void FormatsNumbersAndDatesWithTheInvariantCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // es-CO writes "1.234,5"; XML-RPC needs "1234.5" whatever the machine's culture is.
            CultureInfo.CurrentCulture = new CultureInfo("es-CO");

            Assert.Equal("<value><double>1234.5</double></value>", ValueXml(1234.5m));
            Assert.Equal("<value><double>85.25</double></value>", ValueXml(85.25));
            Assert.Equal("<value><string>2026-10-04</string></value>", ValueXml(new DateOnly(2026, 10, 4)));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ConvertsLocalDateTimesToUtc()
    {
        var local = new DateTime(2026, 10, 4, 8, 0, 0, DateTimeKind.Local);
        var expected = local.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        Assert.Equal($"<value><string>{expected}</string></value>", ValueXml(local));
    }

    [Fact]
    public void WritesDictionariesAsStructsAndCollectionsAsArrays()
    {
        var value = new Dictionary<string, object?> { ["name"] = "Acme", ["tag_ids"] = new List<int> { 1, 2 } };

        Assert.Equal(
            "<value><struct>" +
            "<member><name>name</name><value><string>Acme</string></value></member>" +
            "<member><name>tag_ids</name><value><array><data><value><int>1</int></value><value><int>2</int></value></data></array></value></member>" +
            "</struct></value>",
            ValueXml(value));
    }

    [Fact]
    public void WritesDomainsAndReferencesAsThePlainValuesOdooExpects()
    {
        var domain = OdooDomain.Where("country_id", "=", new OdooReference(49, "Colombia")).Or("id", "in", new[] { 1, 2 });

        Assert.Equal("['|', ['country_id', '=', 49], ['id', 'in', [1, 2]]]", Py.Repr(RoundTrip(domain)));
    }

    [Fact]
    public void WritesDictionariesWithNonObjectValues()
    {
        Assert.Equal("{'quantity': 2.0}", Py.Repr(RoundTrip(new Dictionary<string, double> { ["quantity"] = 2 })));
    }

    public static IEnumerable<object[]> UnsupportedValues =>
    [
        [Guid.Empty],
        [DayOfWeek.Monday],
        [double.NaN],
        [double.PositiveInfinity],
        [ulong.MaxValue],
        [new object()],
        [new Dictionary<int, string> { [1] = "one" }],
    ];

    [Theory]
    [MemberData(nameof(UnsupportedValues))]
    public void RejectsValuesXmlRpcCannotRepresent(object value)
    {
        Assert.Throws<NotSupportedException>(() => XmlRpcSerializer.SerializeMethodCall("method", [value]));
    }

    [Fact]
    public void RejectsReferenceCycles()
    {
        var list = new List<object>();
        list.Add(list);

        Assert.Throws<InvalidOperationException>(() => XmlRpcSerializer.SerializeMethodCall("method", [list]));
    }

    private static string ValueXml(object? value)
    {
        var xml = Encoding.UTF8.GetString(XmlRpcSerializer.SerializeMethodCall("method", [value]));
        var start = xml.IndexOf("<param>", StringComparison.Ordinal) + "<param>".Length;
        var end = xml.LastIndexOf("</param>", StringComparison.Ordinal);
        return xml[start..end];
    }

    private static object? RoundTrip(object? value)
    {
        var document = XDocument.Parse(
            Encoding.UTF8.GetString(XmlRpcSerializer.SerializeMethodCall("method", [value])),
            LoadOptions.PreserveWhitespace);
        return XmlRpcDeserializer.ParseValue(document.Root!.Element("params")!.Element("param")!.Element("value")!);
    }
}
