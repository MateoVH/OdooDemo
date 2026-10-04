namespace OdooConnector.Tests;

public sealed class OdooRecordTests
{
    [Fact]
    public void FalseMeansEmpty()
    {
        var record = Record(("email", false), ("country_id", false), ("invoice_date", false), ("write_date", false), ("tax_ids", false));

        Assert.Null(record.GetString("email"));
        Assert.Null(record.GetReference("country_id"));
        Assert.Null(record.GetDate("invoice_date"));
        Assert.Null(record.GetDateTime("write_date"));
        Assert.Empty(record.GetIds("tax_ids"));
        Assert.False(record.HasValue("email"));
    }

    [Fact]
    public void ReadsScalarFields()
    {
        var record = Record(("id", 7), ("name", "Acme"), ("is_company", true), ("color", 3));

        Assert.Equal(7, record.Id);
        Assert.Equal("Acme", record.GetString("name"));
        Assert.True(record.GetBoolean("is_company"));
        Assert.Equal(3, record.GetInt32("color"));
        Assert.True(record.HasValue("name"));
    }

    [Fact]
    public void ReadsMany2OneFieldsAsReferences()
    {
        var record = Record(("country_id", new object?[] { 49, "Colombia" }));

        Assert.Equal(new OdooReference(49, "Colombia"), record.GetReference("country_id"));
    }

    [Fact]
    public void ReadsX2ManyFieldsAsIds()
    {
        var record = Record(("tax_ids", new object?[] { 1, 5 }));

        Assert.Equal(new[] { 1, 5 }, record.GetIds("tax_ids"));
    }

    [Fact]
    public void ReadsAmountsAsDecimalsWithoutBinaryNoise()
    {
        var record = Record(("amount_total", 99.99), ("amount_tax", 0.1 + 0.2), ("quantity", 3), ("discount", false));

        Assert.Equal(99.99m, record.GetDecimal("amount_total"));
        Assert.Equal(0.3m, record.GetDecimal("amount_tax"));
        Assert.Equal(3m, record.GetDecimal("quantity"));
        Assert.Equal(0m, record.GetDecimal("discount"));
        Assert.Equal(99.99, record.GetDouble("amount_total"));
    }

    [Fact]
    public void ReadsDatesAndUtcDateTimes()
    {
        var record = Record(("invoice_date", "2026-10-04"), ("write_date", "2026-10-04 13:45:00"));

        Assert.Equal(new DateOnly(2026, 10, 4), record.GetDate("invoice_date"));

        var writeDate = record.GetDateTime("write_date");
        Assert.Equal(new DateTime(2026, 10, 4, 13, 45, 0), writeDate);
        Assert.Equal(DateTimeKind.Utc, writeDate!.Value.Kind);
    }

    [Fact]
    public void ExposesTheRawFields()
    {
        var record = Record(("id", 7), ("name", "Acme"));

        Assert.Equal(new[] { "id", "name" }, record.Fields.Keys);
        Assert.Equal("Acme", record["name"]);
    }

    [Fact]
    public void ExplainsWhichFieldIsMissing()
    {
        var exception = Assert.Throws<KeyNotFoundException>(() => Record(("id", 7)).GetString("email"));

        Assert.Contains("'email'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("fields requested", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplainsWhenAFieldHasAnotherType()
    {
        var record = Record(("country_id", new object?[] { 49, "Colombia" }));

        var exception = Assert.Throws<InvalidCastException>(() => record.GetInt32("country_id"));

        Assert.Contains("'country_id'", exception.Message, StringComparison.Ordinal);
    }

    private static OdooRecord Record(params (string Field, object? Value)[] fields) =>
        new(fields.ToDictionary(field => field.Field, field => field.Value));
}
