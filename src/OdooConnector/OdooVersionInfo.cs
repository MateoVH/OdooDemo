using System.Globalization;
using OdooConnector.Resources;

namespace OdooConnector;

/// <summary>Version information returned by <c>common.version()</c>, which does not require authentication.</summary>
/// <param name="ServerVersion">Full version, e.g. <c>17.0+e</c> or <c>saas~17.4+e</c>.</param>
/// <param name="ServerSerie">Release series, e.g. <c>17.0</c> or <c>saas~17.4</c>.</param>
/// <param name="ProtocolVersion">Version of the external API protocol.</param>
public sealed record OdooVersionInfo(string ServerVersion, string ServerSerie, int ProtocolVersion)
{
    /// <summary>Major version (17 for both <c>17.0</c> and <c>saas~17.4</c>), or <see langword="null"/> if unknown.</summary>
    public int? MajorVersion
    {
        get
        {
            var serie = ServerSerie.StartsWith("saas~", StringComparison.Ordinal) ? ServerSerie["saas~".Length..] : ServerSerie;
            var major = serie.Split('.')[0];
            return int.TryParse(major, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
        }
    }

    internal static OdooVersionInfo FromRpc(object? value)
    {
        if (value is not IReadOnlyDictionary<string, object?> info)
        {
            throw new OdooException(Strings.UnexpectedResult("common", "version", value?.GetType().Name ?? "null"));
        }

        return new OdooVersionInfo(
            info.GetValueOrDefault("server_version") as string ?? string.Empty,
            info.GetValueOrDefault("server_serie") as string ?? string.Empty,
            info.GetValueOrDefault("protocol_version") as int? ?? 0);
    }
}
