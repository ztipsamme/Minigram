using Azure.Storage.Blobs;

static class BildEndpoints
{
    internal static void Map(
        WebApplication app,
        BildStore store,
        BlobContainerClient? blobContainerClient,
        string? sasUrl,
        Dictionary<string, string> rollMappning)
    {
        app.MapGet("/bilder", async () =>
        {
            List<Bild> res = [];

            if (blobContainerClient is null)
            {
                Console.WriteLine("BLOB_SAS_URL saknas — använder mockdata.");
                return Results.Ok(store.Bilder);
            }
            else
            {
                Console.WriteLine("BLOB_SAS_URL finns — använder Azure Blob Storage.");
            }

            await foreach (var blobItem in blobContainerClient.GetBlobsAsync())
            {
                var blob = blobContainerClient.GetBlobClient(blobItem.Name);

                var url = $"{blob.Uri}?{new Uri(sasUrl).Query.TrimStart('?')}";

                res.Add(
                    new Bild(
                        Id: 0,
                        Namn: blobItem.Name,
                        Caption: "",
                        Taggar: [],
                        Url: url)
                );
            }

            return Results.Ok(res);
        })
           .WithName("HamtaBilder")
           .WithSummary("Hämta alla bilder — alla roller");

        app.MapGet("/bilder/{id:int}", (int id) =>
        {
            var bild = store.Bilder.FirstOrDefault(b => b.Id == id);
            return bild is not null ? Results.Ok(bild) : Results.NotFound();
        })
        .WithName("HamtaBild")
        .WithSummary("Hämta en specifik bild — alla roller");

        app.MapPost("/bilder", (NyBild ny, HttpRequest req) =>
        {
            if (!EasyAuthAuthorization.HarBehorighet(EasyAuthAuthorization.HamtaRoll(req, rollMappning), "Fotograf")) return Results.StatusCode(403);
            var bild = new Bild(store.NastaBildId++, ny.Namn, ny.Caption, ny.Taggar ?? [], ny.Url);
            store.Bilder.Add(bild);
            return Results.Created($"/bilder/{bild.Id}", bild);
        })
        .WithName("LaddaUppBild")
        .WithSummary("Lägg till bild via URL — kräver Fotograf eller Admin");

        app.MapPut("/bilder/{id:int}", (int id, BildUpdate update, HttpRequest req) =>
        {
            if (!EasyAuthAuthorization.HarBehorighet(EasyAuthAuthorization.HamtaRoll(req, rollMappning), "Fotograf")) return Results.StatusCode(403);
            var index = store.Bilder.FindIndex(b => b.Id == id);
            if (index < 0) return Results.NotFound();
            store.Bilder[index] = store.Bilder[index] with
            {
                Caption = update.Caption ?? store.Bilder[index].Caption,
                Taggar = update.Taggar ?? store.Bilder[index].Taggar
            };
            return Results.Ok(store.Bilder[index]);
        })
        .WithName("UppdateraBild")
        .WithSummary("Uppdatera bild — kräver Fotograf eller Admin");

        app.MapDelete("/bilder/{id:int}", (int id, HttpRequest req) =>
        {
            if (!EasyAuthAuthorization.HarBehorighet(EasyAuthAuthorization.HamtaRoll(req, rollMappning), "Admin")) return Results.StatusCode(403);
            var bild = store.Bilder.FirstOrDefault(b => b.Id == id);
            if (bild is null) return Results.NotFound();
            store.Bilder.Remove(bild);
            return Results.NoContent();
        })
        .WithName("RaderaBild")
        .WithSummary("Radera bild — kräver Admin");
    }
}
