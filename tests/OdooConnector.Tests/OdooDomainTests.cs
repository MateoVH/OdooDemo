using OdooConnector.Tests.Infrastructure;
using OdooConnector.XmlRpc;

namespace OdooConnector.Tests;

public sealed class OdooDomainTests
{
    private static readonly OdooDomain IsCompany = OdooDomain.Where("is_company", "=", true);
    private static readonly OdooDomain InColombia = OdooDomain.Where("country_id.code", "=", "CO");
    private static readonly OdooDomain HasEmail = OdooDomain.Where("email", "!=", false);

    [Fact]
    public void EmptyDomainMatchesEverything()
    {
        Assert.True(OdooDomain.Empty.IsEmpty);
        Assert.Equal("[]", OdooDomain.Empty.ToString());
        Assert.Equal("[]", Py.Repr(Terms(OdooDomain.Empty)));
    }

    [Fact]
    public void WhereCreatesASingleCondition()
    {
        Assert.False(IsCompany.IsEmpty);
        Assert.Equal("[('is_company', '=', True)]", IsCompany.ToString());
        Assert.Equal("[['is_company', '=', True]]", Py.Repr(Terms(IsCompany)));
    }

    [Fact]
    public void CombinesConditionsInPrefixNotation()
    {
        Assert.Equal(
            "['&', ('is_company', '=', True), ('country_id.code', '=', 'CO')]",
            IsCompany.And(InColombia).ToString());

        // (A and B) or C
        Assert.Equal(
            "['|', '&', ('is_company', '=', True), ('country_id.code', '=', 'CO'), ('email', '!=', False)]",
            IsCompany.And(InColombia).Or(HasEmail).ToString());

        // A and (B or C)
        Assert.Equal(
            "['&', ('is_company', '=', True), '|', ('country_id.code', '=', 'CO'), ('email', '!=', False)]",
            IsCompany.And(InColombia.Or(HasEmail)).ToString());
    }

    [Fact]
    public void NotNegatesTheWholeDomain()
    {
        Assert.Equal(
            "['!', '|', ('is_company', '=', True), ('country_id.code', '=', 'CO')]",
            OdooDomain.Not(IsCompany.Or(InColombia)).ToString());
    }

    [Fact]
    public void EmptyDomainsAreIgnoredWhenCombining()
    {
        Assert.Same(IsCompany, OdooDomain.Empty.And(IsCompany));
        Assert.Same(IsCompany, IsCompany.Or(OdooDomain.Empty));
        Assert.Equal(IsCompany.And(InColombia).ToString(), OdooDomain.AllOf(OdooDomain.Empty, IsCompany, InColombia).ToString());
        Assert.Equal(IsCompany.Or(InColombia).ToString(), OdooDomain.AnyOf(IsCompany, OdooDomain.Empty, InColombia).ToString());
        Assert.True(OdooDomain.AnyOf().IsEmpty);
    }

    [Fact]
    public void ShorthandOverloadsAddConditions()
    {
        Assert.Equal(IsCompany.And(InColombia).ToString(), IsCompany.And("country_id.code", "=", "CO").ToString());
        Assert.Equal(IsCompany.Or(HasEmail).ToString(), IsCompany.Or("email", "!=", false).ToString());
    }

    [Fact]
    public void IsImmutable()
    {
        var combined = IsCompany.And(InColombia);

        Assert.Equal("[('is_company', '=', True)]", IsCompany.ToString());
        Assert.NotSame(IsCompany, combined);
    }

    [Fact]
    public void SendsNullAsFalse()
    {
        var domain = OdooDomain.Where("parent_id", "=", null);

        Assert.Equal("[('parent_id', '=', False)]", domain.ToString());
        Assert.Equal("[['parent_id', '=', False]]", Py.Repr(Terms(domain)));
    }

    [Fact]
    public void ToStringFormatsValuesLikePython()
    {
        var domain = OdooDomain.Where("name", "ilike", "O'Brien")
            .And("id", "in", new[] { 1, 2, 3 })
            .And("invoice_date", ">=", new DateOnly(2026, 1, 1))
            .And("amount_total", ">", 99.5m);

        Assert.Equal(
            "['&', '&', '&', ('name', 'ilike', 'O\\'Brien'), ('id', 'in', [1, 2, 3]), ('invoice_date', '>=', '2026-01-01'), ('amount_total', '>', 99.5)]",
            domain.ToString());
    }

    [Theory]
    [InlineData("", "=")]
    [InlineData(" ", "=")]
    [InlineData("name", "")]
    public void RejectsMissingFieldOrOperator(string field, string @operator)
    {
        Assert.Throws<ArgumentException>(() => OdooDomain.Where(field, @operator, "value"));
    }

    [Fact]
    public void CannotNegateAnEmptyDomain()
    {
        Assert.Throws<ArgumentException>(() => OdooDomain.Not(OdooDomain.Empty));
    }

    private static object? Terms(OdooDomain domain) => RoundTrip(((IXmlRpcValue)domain).ToXmlRpcValue());

    // Serializes and parses the value, so the assertions check what actually goes over the wire.
    private static object? RoundTrip(object? value)
    {
        var body = System.Text.Encoding.UTF8.GetString(XmlRpcSerializer.SerializeMethodCall("method", [value]));
        return RpcCall.Parse(new Uri("https://odoo.test/xmlrpc/2/object"), body).Params[0];
    }
}
