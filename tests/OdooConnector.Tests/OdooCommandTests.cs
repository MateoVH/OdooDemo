namespace OdooConnector.Tests;

public sealed class OdooCommandTests
{
    private static readonly Dictionary<string, object?> Values = new() { ["name"] = "Line" };

    [Fact]
    public void BuildsTheSameTuplesAsPythonsCommandClass()
    {
        Assert.Equal(new object[] { 0, 0, Values }, OdooCommand.Create(Values));
        Assert.Equal(new object[] { 1, 7, Values }, OdooCommand.Update(7, Values));
        Assert.Equal(new object[] { 2, 7, 0 }, OdooCommand.Delete(7));
        Assert.Equal(new object[] { 3, 7, 0 }, OdooCommand.Unlink(7));
        Assert.Equal(new object[] { 4, 7, 0 }, OdooCommand.Link(7));
        Assert.Equal(new object[] { 5, 0, 0 }, OdooCommand.Clear());
    }

    [Fact]
    public void SetCopiesTheIds()
    {
        var ids = new List<int> { 1, 2 };

        var command = OdooCommand.Set(ids);
        ids.Add(3);

        Assert.Equal(6, command[0]);
        Assert.Equal(0, command[1]);
        Assert.Equal(new[] { 1, 2 }, Assert.IsType<int[]>(command[2]));
    }
}
