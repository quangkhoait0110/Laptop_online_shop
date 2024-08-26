using Minio;
using Microsoft.Extensions.Configuration;
using Minio.DataModel.Args;

namespace ProductApi1.Services
{
    public class MinioService
    {
        private readonly IMinioClient _minioClient;
        public string BucketName { get; }

        public MinioService(IConfiguration configuration)
        {
            var minioConfig = configuration.GetSection("Minio");
            _minioClient = new MinioClient()
                .WithEndpoint(minioConfig["Endpoint"] ?? "10.70.123.76:9000")
                .WithCredentials(minioConfig["AccessKey"], minioConfig["SecretKey"])
                .WithSSL(false) 
                .Build();
            BucketName = minioConfig["BucketName"] ?? throw new InvalidOperationException("BucketName is not configured");
        }

        public async Task<string> GeneratePresignedUrlForUpload(string objectName, int expirySeconds = 3600)
        {
            var presignedUrl = await _minioClient.PresignedPutObjectAsync(
                new PresignedPutObjectArgs()
                    .WithBucket(BucketName)
                    .WithObject(objectName)
                    .WithExpiry(expirySeconds));
            return presignedUrl;
        }

        public async Task<string> GeneratePresignedUrlForReading(string objectName, int expirySeconds = 3600)
        {
            var presignedUrl = await _minioClient.PresignedGetObjectAsync(
                new PresignedGetObjectArgs()
                    .WithBucket(BucketName)
                    .WithObject(objectName)
                    .WithExpiry(expirySeconds));
            return presignedUrl;
        }
    }
}