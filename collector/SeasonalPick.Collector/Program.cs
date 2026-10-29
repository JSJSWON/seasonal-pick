using Microsoft.Extensions.Configuration;

// Entry point for the collector.
//
// At this stage the program only proves that configuration loading works.
// Non-secret settings come from appsettings.json, which is version-controlled;
// the aT service key comes from User Secrets, which is stored outside the
// repository under ~/.microsoft/usersecrets/<UserSecretsId>/. Later providers
// (environment variables for deployment) can be layered on top without
// changing the call sites, because everything is read through IConfiguration.

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddUserSecrets<Program>()
    .Build();

var baseUrl = configuration["At:BaseUrl"];
var serviceKey = configuration["At:ServiceKey"];

Console.WriteLine($"At:BaseUrl    = {baseUrl ?? "(없음)"}");
Console.WriteLine($"At:ServiceKey = {Mask(serviceKey)}");

if (string.IsNullOrWhiteSpace(serviceKey))
{
    Console.WriteLine();
    Console.WriteLine("서비스 키가 등록되지 않았습니다. 프로젝트 폴더에서:");
    Console.WriteLine("  dotnet user-secrets set \"At:ServiceKey\" \"<발급받은 키>\"");
    return 1;
}

return 0;

/// <summary>
/// Renders a secret for printing. Keeps the first and last four characters and
/// replaces the middle with asterisks, so the value can be recognised as the
/// right one without being disclosed in a terminal or a log.
/// </summary>
/// <param name="value">The secret to render. May be null or blank.</param>
/// <returns>
/// A masked form of <paramref name="value"/>, or a placeholder when it is
/// missing. Values of eight characters or fewer are masked completely, since
/// keeping eight characters of them would reveal the whole secret.
/// </returns>
static string Mask(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "(없음)";
    }

    return value.Length <= 8
        ? new string('*', value.Length)
        : $"{value[..4]}{new string('*', value.Length - 8)}{value[^4..]}";
}
