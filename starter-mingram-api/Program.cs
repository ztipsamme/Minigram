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

using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMinGramCors(builder.Configuration);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("MinGramPolicy");

var rollMappning = RoleMappingConfiguration.Load(builder.Configuration);

var sasUrl = Environment.GetEnvironmentVariable("BLOB_SAS_URL");

var blobContainerClient = string.IsNullOrWhiteSpace(sasUrl)
    ? null
    : new BlobContainerClient(new Uri(sasUrl));

var bildStore = new BildStore();

BildEndpoints.Map(
    app,
    bildStore,
    blobContainerClient,
    sasUrl,
    rollMappning
);
UserEndpoints.Map(app, rollMappning);

app.Run();

