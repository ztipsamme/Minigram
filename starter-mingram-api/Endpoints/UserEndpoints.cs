using System.Text.Json;

static class UserEndpoints
{
    internal static void Map(
        WebApplication app,
        Dictionary<string, string> rollMappning)
    {
        app.MapGet("/jag", (HttpRequest req) =>
        {
            var header = req.Headers["X-MS-CLIENT-PRINCIPAL"].FirstOrDefault();
            return Results.Ok(new
            {
                roll = EasyAuthAuthorization.HamtaRoll(req, rollMappning),
                harPrincipal = !string.IsNullOrEmpty(header),
                email = EasyAuthAuthorization.HamtaEmail(req)
            });
        })
        .WithName("VemArJag")
        .WithSummary("Visa inloggad mail och mappad roll");

        app.MapGet("/debug-roll", (IConfiguration config) =>
        {
            var raw = config["RollMappningJson"];

            var mapping =
                string.IsNullOrWhiteSpace(raw)
                    ? new Dictionary<string, string>()
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(raw)
                      ?? new Dictionary<string, string>();

            var email = "osiss121@gmail.com";

            return Results.Json(new
            {
                raw,
                count = mapping.Count,
                keys = mapping.Keys.ToArray(),
                containsExact = mapping.ContainsKey(email),
                role = mapping.TryGetValue(email, out var role)
                    ? role
                    : null
            });
        });
    }
}
