using System.Text;
using System.Text.Json;

static class EasyAuthAuthorization
{
    internal static string? HamtaEmail(HttpRequest request)
    {
        var header = request.Headers["X-MS-CLIENT-PRINCIPAL"].FirstOrDefault();
        if (string.IsNullOrEmpty(header)) return null;

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(header));
            using var doc = JsonDocument.Parse(json);

            foreach (var claim in doc.RootElement.GetProperty("claims").EnumerateArray())
            {
                var typ = claim.TryGetProperty("typ", out var t1) ? t1.GetString()
                        : claim.TryGetProperty("type", out var t2) ? t2.GetString()
                        : null;

                if (typ is "preferred_username"
                    or "upn"
                    or "emails"
                    or "email"
                    or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn"
                    or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"
                    or "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")
                {
                    var val = claim.GetProperty("val").GetString();
                    if (!string.IsNullOrWhiteSpace(val) && val.Contains('@'))
                        return val;
                }
            }
        }
        catch { }

        return null;
    }

    internal static string HamtaRoll(HttpRequest request, Dictionary<string, string> rollMappning)
    {
        var header = request.Headers["X-MS-CLIENT-PRINCIPAL"].FirstOrDefault();
        if (string.IsNullOrEmpty(header)) return "Betraktare";

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(header));
            using var doc = JsonDocument.Parse(json);
            string? email = null;

            foreach (var claim in doc.RootElement.GetProperty("claims").EnumerateArray())
            {
                var type = claim.GetProperty("typ").GetString();
                if (type == "email"
                    || type == "preferred_username"
                    || type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"
                    || type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn")
                {
                    email = claim.GetProperty("val").GetString()?.Trim();
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(email) && rollMappning.TryGetValue(email, out var roll))
                return roll;
        }
        catch
        {
            // Invalid/missing Easy Auth header
        }

        return "Betraktare";
    }

    internal static bool HarBehorighet(string roll, string kravRoll) => (roll, kravRoll) switch
    {
        (_, "Betraktare") => true,
        ("Fotograf" or "Admin", "Fotograf") => true,
        ("Admin", "Admin") => true,
        _ => false
    };
}
