// Program.cs — MinGram API
// ASP.NET Core Minimal API: endpoints definieras direkt här, inga controllers.
//
// Starta lokalt:  dotnet run
// Swagger UI:     https://localhost:{port}/swagger
//
// v35 — Azure-konfiguration (görs i portalen, inte i koden):
// 1. CORS: App Service → API → CORS → lägg till din frontend-URL
// 2. Easy Auth: App Service → Authentication → Add identity provider → Microsoft
//    Välj din Entra ID-tenant. Alla anrop kräver nu inloggning.
// 3. App-roller i Entra ID: gå till App registrations → din app → App roles
//    Skapa rollerna Betraktare, Fotograf, Admin.
//    Tilldela dem till dina Entra ID-användare under Enterprise applications.
//
// Bilder kan skickas som URL (POST /bilder) eller laddas upp till Blob Storage
// (POST /bilder/uppladdning). Connection string sätts i Azure App Settings.

using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS — hanteras primärt i Azure Portal: App Service → API → CORS
// Lägg till din frontend-URL där, så slipper du ändra och redeploya koden.
// Den här koden hanterar CORS lokalt under utveckling.
builder.Services.AddCors(options =>
{
    options.AddPolicy("MinGramPolicy", policy =>
    {
        var origins = builder.Configuration
                             .GetSection("AllowedOrigins")
                             .Get<string[]>() ?? [];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("MinGramPolicy");

// ======================================================
// Rollmappning — ersätter Entra ID App roles
// Skolkontot tillåter inte App roles/Enterprise application-tilldelning,
// så rollen mappas här istället, baserat på den autentiserade
// användarens e-post (levererad äkta av Easy Auth).
// ======================================================

var rollMappningJson = builder.Configuration["RollMappningJson"];

var rollMappning =
    string.IsNullOrEmpty(rollMappningJson)
        ? new Dictionary<string, string>()
        : JsonSerializer.Deserialize<Dictionary<string, string>>(rollMappningJson)
          ?? new Dictionary<string, string>();

// Blob Storage — connection string ligger i App Settings i Azure
var storageConn = builder.Configuration["AzureStorageConnectionString"];
var containerNamn = builder.Configuration["AzureStorageContainer"] ?? "bilder";
BlobContainerClient? blobContainer = null;

if (!string.IsNullOrWhiteSpace(storageConn))
{
    var blobService = new BlobServiceClient(storageConn);
    blobContainer = blobService.GetBlobContainerClient(containerNamn);
}

// -------------------------------------------------------
// In-memory datastore med seed-data
// Datan nollställs vid omstart — filerna ligger kvar i Blob Storage
// -------------------------------------------------------

var bilder = new List<Bild>
{
    new(1, "demo.jpg", "Demobild — ersätt med din egen", ["demo", "placeholder"],
        "https://placehold.co/400x300?text=MinGram")
};
var nastaBildId = 2;

// ======================================================
// Bilder
// ======================================================

// Alla roller får se bilder
app.MapGet("/bilder", () => bilder)
   .WithName("HamtaBilder")
   .WithSummary("Hämta alla bilder — alla roller");

app.MapGet("/bilder/{id:int}", (int id) =>
{
    var b = bilder.FirstOrDefault(b => b.Id == id);
    return b is not null ? Results.Ok(b) : Results.NotFound();
})
.WithName("HamtaBild")
.WithSummary("Hämta en specifik bild — alla roller");

// Fotograf och Admin får lägga till bild via URL (som tidigare)
app.MapPost("/bilder", (NyBild ny, HttpRequest req) =>
{
    if (!HarBehorighet(HamtaRoll(req), "Fotograf")) return Results.StatusCode(403);
    var b = new Bild(nastaBildId++, ny.Namn, ny.Caption, ny.Taggar ?? [], ny.Url);
    bilder.Add(b);
    return Results.Created($"/bilder/{b.Id}", b);
})
.WithName("LaddaUppBild")
.WithSummary("Lägg till bild via URL — kräver Fotograf eller Admin");

// Fotograf och Admin får ladda upp en riktig fil till Blob Storage
app.MapPost("/bilder/uppladdning", async (
    HttpRequest req,
    IFormFile fil,
    [FromForm] string? caption,
    [FromForm] string? namn,
    [FromForm] string? taggar) =>
{
    if (!HarBehorighet(HamtaRoll(req), "Fotograf")) return Results.StatusCode(403);

    if (blobContainer is null)
        return Results.Problem("Blob Storage är inte konfigurerat (AzureStorageConnectionString saknas).");

    if (fil is null || fil.Length == 0)
        return Results.BadRequest("Skicka en fil i fältet 'fil'.");

    caption = string.IsNullOrWhiteSpace(caption) ? fil.FileName : caption;
    namn = string.IsNullOrWhiteSpace(namn) ? fil.FileName : namn;

    var taggLista = string.IsNullOrWhiteSpace(taggar)
        ? new List<string>()
        : taggar.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

    await blobContainer.CreateIfNotExistsAsync();

    // Unikt filnamn så vi inte skriver över gamla bilder
    var blobNamn = $"{Guid.NewGuid():N}-{Path.GetFileName(fil.FileName)}";
    var blob = blobContainer.GetBlobClient(blobNamn);

    // Sätt content-type så bilden visas i webbläsaren istället för att laddas ner
    var contentType = string.IsNullOrWhiteSpace(fil.ContentType)
        ? "application/octet-stream"
        : fil.ContentType;

    var uploadOptions = new BlobUploadOptions
    {
        HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
    };

    await using (var stream = fil.OpenReadStream())
    {
        await blob.UploadAsync(stream, uploadOptions);
    }

    var url = SkapaLasbarUrl(blob);
    var b = new Bild(nastaBildId++, namn, caption, taggLista, url);
    bilder.Add(b);
    return Results.Created($"/bilder/{b.Id}", b);
})
.DisableAntiforgery()
.WithName("LaddaUppBildFil")
.WithSummary("Ladda upp bildfil till Blob Storage — kräver Fotograf eller Admin")
.Accepts<IFormFile>("multipart/form-data");

// Visar vilken mail/roll Easy Auth ger dig (bra när man felsöker)
app.MapGet("/jag", (HttpRequest req) =>
{
    var header = req.Headers["X-MS-CLIENT-PRINCIPAL"].FirstOrDefault();
    return Results.Ok(new
    {
        roll = HamtaRoll(req),
        harPrincipal = !string.IsNullOrEmpty(header),
        email = HamtaEmail(req)
    });
})
.WithName("VemArJag")
.WithSummary("Visa inloggad mail och mappad roll");

// Fotograf och Admin får uppdatera caption och taggar
app.MapPut("/bilder/{id:int}", (int id, BildUpdate update, HttpRequest req) =>
{
    if (!HarBehorighet(HamtaRoll(req), "Fotograf")) return Results.StatusCode(403);
    var index = bilder.FindIndex(b => b.Id == id);
    if (index < 0) return Results.NotFound();
    bilder[index] = bilder[index] with
    {
        Caption = update.Caption ?? bilder[index].Caption,
        Taggar = update.Taggar ?? bilder[index].Taggar
    };
    return Results.Ok(bilder[index]);
})
.WithName("UppdateraBild")
.WithSummary("Uppdatera bild — kräver Fotograf eller Admin");

// Bara Admin får ta bort bilder — testa med Postman som Betraktare för att se 403
app.MapDelete("/bilder/{id:int}", (int id, HttpRequest req) =>
{
    if (!HarBehorighet(HamtaRoll(req), "Admin")) return Results.StatusCode(403);
    var b = bilder.FirstOrDefault(b => b.Id == id);
    if (b is null) return Results.NotFound();
    bilder.Remove(b);
    return Results.NoContent();
})
.WithName("RaderaBild")
.WithSummary("Radera bild — kräver Admin");

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

app.Run();

// ======================================================
// Blob-hjälp
// ======================================================

// Storage är privat, så vi ger en SAS-länk som går att öppna i webbläsaren
string SkapaLasbarUrl(BlobClient blob)
{
    if (blob.CanGenerateSasUri)
    {
        var sas = new BlobSasBuilder(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddYears(1))
        {
            BlobContainerName = blob.BlobContainerName,
            BlobName = blob.Name
        };
        return blob.GenerateSasUri(sas).ToString();
    }

    return blob.Uri.ToString();
}

// ======================================================
// Rollkontroll
// ======================================================

// Läser rollen ur Easy Auth-headern som Azure injicerar efter inloggning.
// Lokalt (utan Easy Auth): returnerar "Admin" så Swagger fungerar utan
// inloggning.

string? HamtaEmail(HttpRequest request)
{
    var header = request.Headers["X-MS-CLIENT-PRINCIPAL"].FirstOrDefault();
    if (string.IsNullOrEmpty(header)) return null;

    try
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(header));
        using var doc = JsonDocument.Parse(json);

        // Easy Auth använder oftast "typ", inte "type"
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

string HamtaRoll(HttpRequest request)
{
    var header = request.Headers["X-MS-CLIENT-PRINCIPAL"].FirstOrDefault();

    if (string.IsNullOrEmpty(header))
        return "Betraktare";

    try
    {
        var json = Encoding.UTF8.GetString(
            Convert.FromBase64String(header)
        );

        using var doc = JsonDocument.Parse(json);

        string? email = null;

        foreach (var claim in doc.RootElement
            .GetProperty("claims")
            .EnumerateArray())
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

        if (!string.IsNullOrWhiteSpace(email) &&
            rollMappning.TryGetValue(email, out var roll))
        {
            return roll;
        }
    }
    catch
    {
        // Invalid/missing Easy Auth header
    }

    return "Betraktare";
}

// Kontrollerar om en roll har tillräcklig behörighet.
// Hierarki: Betraktare < Fotograf < Admin
bool HarBehorighet(string roll, string kravRoll) => (roll, kravRoll) switch
{
    (_, "Betraktare") => true,
    ("Fotograf" or "Admin", "Fotograf") => true,
    ("Admin", "Admin") => true,
    _ => false
};

// ======================================================
// Datamodeller
// ======================================================

record Bild(int Id, string Namn, string Caption, List<string> Taggar, string Url);

record NyBild(string Namn, string Caption, List<string>? Taggar, string Url);
record BildUpdate(string? Caption, List<string>? Taggar);
