using Microsoft.Extensions.Options;
using OdooConnector.Resources;

namespace OdooConnector;

/// <summary>Validates <see cref="OdooOptions"/> both for dependency injection and for direct construction.</summary>
internal sealed class OdooOptionsValidator : IValidateOptions<OdooOptions>
{
    public ValidateOptionsResult Validate(string? name, OdooOptions options)
    {
        var errors = GetErrors(options);
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    public static List<string> GetErrors(OdooOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();
        if (options.Url is null)
        {
            errors.Add(Strings.UrlRequired);
        }
        else if (!options.Url.IsAbsoluteUri
                 || (options.Url.Scheme != Uri.UriSchemeHttps && options.Url.Scheme != Uri.UriSchemeHttp))
        {
            errors.Add(Strings.UrlNotHttp(options.Url));
        }
        else if (!string.IsNullOrEmpty(options.Url.Query) || !string.IsNullOrEmpty(options.Url.Fragment))
        {
            errors.Add(Strings.UrlHasQuery(options.Url));
        }

        if (string.IsNullOrWhiteSpace(options.Database))
        {
            errors.Add(Strings.DatabaseRequired);
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            errors.Add(Strings.UsernameRequired);
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            errors.Add(Strings.ApiKeyRequired);
        }

        return errors;
    }
}
