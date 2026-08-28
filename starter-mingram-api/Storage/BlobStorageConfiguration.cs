using Azure.Storage.Blobs;

static class BlobStorageConfiguration
{
    internal static BlobContainerClient? CreateContainer(IConfiguration configuration)
    {
        var connectionString = configuration["AzureStorageConnectionString"];
        var containerName = configuration["AzureStorageContainer"] ?? "bilder";

        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var blobService = new BlobServiceClient(connectionString);
        return blobService.GetBlobContainerClient(containerName);
    }
}
