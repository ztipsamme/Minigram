using Azure.Storage.Blobs;
using Azure.Storage.Sas;

static class BlobUrlFactory
{
    internal static string CreateReadableUrl(BlobClient blob)
    {
        if (blob.CanGenerateSasUri)
        {
            var sas = new BlobSasBuilder(
                BlobSasPermissions.Read,
                DateTimeOffset.UtcNow.AddYears(1))
            {
                BlobContainerName = blob.BlobContainerName,
                BlobName = blob.Name
            };

            return blob.GenerateSasUri(sas).ToString();
        }

        return blob.Uri.ToString();
    }
}
