using System.Text.Json;

static class RoleMappingConfiguration
{
    internal static Dictionary<string, string> Load(IConfiguration configuration)
    {
        var json = configuration["RollMappningJson"];

        return string.IsNullOrEmpty(json)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json)
              ?? new Dictionary<string, string>();
    }
}
