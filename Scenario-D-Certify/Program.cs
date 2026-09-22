// CertifyApi — Scenario D: Digitala certifikat med Azure Blob Storage
// ─────────────────────────────────────────────────────────────────
// Starta lokalt: dotnet run  →  Swagger: http://localhost:5000/swagger
//
// Miljövariabler (Container Apps → Settings → Environment variables):
//   AZURE_STORAGE_URL     https://{ditt-konto}.blob.core.windows.net/
//
// Managed Identity-roller som Container App-identiteten behöver:
//   "Storage Blob Data Contributor" →  på Storage Account
//
// Saknas AZURE_STORAGE_URL → demo-läge (in-memory, nollställs vid omstart)
// ─────────────────────────────────────────────────────────────────

using Azure.Identity;
using Azure.Storage.Blobs;
using System.Text.Json;

var storageUrl = Environment.GetEnvironmentVariable("AZURE_STORAGE_URL");
var azureMode = storageUrl is not null;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new() { Title = "Certify API", Version = "v1" }));
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

BlobContainerClient? blobs = null;
if (azureMode)
{
    blobs = new BlobServiceClient(new Uri(storageUrl!), new DefaultAzureCredential())
        .GetBlobContainerClient("certificates");
    await blobs.CreateIfNotExistsAsync();
}

var certifikat = new Dictionary<string, Certifikat>();

// ── GET /health ──────────────────────────────────────────────────
app.MapGet("/health", () => new { status = "ok", mode = azureMode ? "azure" : "demo" })
   .WithTags("Status").Produces<object>(200);

// ── POST /certificates ───────────────────────────────────────────
app.MapPost("/certificates", async (NyttCertifikat indata) =>
{
    var id = Guid.NewGuid().ToString();
    var url = $"/verify/{id}";
    var c = new Certifikat(id, indata.Mottagare, indata.Kurs, indata.Datum, url, DateTime.UtcNow);

    if (azureMode && blobs is not null)
        await blobs.UploadBlobAsync($"{id}.json", new BinaryData(JsonSerializer.Serialize(c)));

    certifikat[id] = c;
    return Results.Created($"/certificates/{id}", new { id, url, status = "utfärdat" });
})
.WithTags("Certifikat").WithSummary("Utfärda ett nytt certifikat")
.Produces<object>(201);

// ── GET /certificates/{id} ───────────────────────────────────────
app.MapGet("/certificates/{id}", async (string id) =>
{
    if (certifikat.TryGetValue(id, out var cached)) return Results.Ok(cached);
    if (blobs is null) return Results.NotFound();

    var blob = blobs.GetBlobClient($"{id}.json");
    if (!await blob.ExistsAsync()) return Results.NotFound();
    var download = await blob.DownloadContentAsync();
    return Results.Ok(JsonSerializer.Deserialize<Certifikat>(download.Value.Content));
})
.WithTags("Certifikat").WithSummary("Hämta certifikatdata via ID")
.Produces<Certifikat>(200).Produces(404);

// ── GET /verify/{uuid} — publik verifieringsendpoint ─────────────
app.MapGet("/verify/{uuid}", async (string uuid) =>
{
    Certifikat? c = null;

    if (certifikat.TryGetValue(uuid, out var cached))
        c = cached;
    else if (azureMode && blobs is not null)
    {
        var blob = blobs.GetBlobClient($"{uuid}.json");
        if (await blob.ExistsAsync())
        {
            var download = await blob.DownloadContentAsync();
            c = JsonSerializer.Deserialize<Certifikat>(download.Value.Content);
        }
    }

    if (c is null)
        return Results.NotFound(new { fel = "Certifikatet hittades inte eller är ogiltigt" });

    return Results.Ok(new
    {
        giltigt = true,
        mottagare = c.Mottagare,
        kurs = c.Kurs,
        datum = c.Datum,
        utfardat = c.Utfardat
    });
})
.WithTags("Verifiering").WithSummary("Publik verifiering — kontrollera om certifikat är äkta")
.Produces<object>(200).Produces(404);

// ── GET /certificates — lista alla (kräver API-nyckel) ───────────
app.MapGet("/certificates", async (HttpRequest req) =>
{
    if (!req.Headers.TryGetValue("X-Api-Key", out var key) || key != "demo-nyckel")
        return Results.Unauthorized();

    if (blobs is null) return Results.Ok(certifikat.Values);

    var ids = new List<string>();
    await foreach (var b in blobs.GetBlobsAsync()) ids.Add(b.Name.Replace(".json", ""));
    return Results.Ok(ids);
})
.WithTags("Certifikat").WithSummary("Lista alla certifikat — kräver X-Api-Key: demo-nyckel i headern")
.Produces<List<string>>(200).Produces(401);

app.Run();

// ── Modeller ─────────────────────────────────────────────────────
record NyttCertifikat(string Mottagare, string Kurs, string Datum);
record Certifikat(string Id, string Mottagare, string Kurs, string Datum,
    string VerifieringsUrl, DateTime Utfardat);