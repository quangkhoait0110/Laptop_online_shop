using Couchbase;
using Couchbase.KeyValue;
using Couchbase.Query;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using ProductApi1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProductApi1.Services
{
    public class CouchbaseService
    {
        private readonly CouchbaseOptions _options;
        private readonly IDistributedCache _cache;
        private ICluster? _cluster;
        private readonly IBucket _bucket;
        private readonly ICouchbaseCollection _collection;

        public CouchbaseService(IOptions<CouchbaseOptions> options , IDistributedCache cache)
        {
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _bucket = GetBucketAsync().GetAwaiter().GetResult();
            _collection = _bucket.DefaultCollection();
        }

        private async Task<ICluster> GetClusterAsync()
        {
            if (_cluster == null)
            {
                _cluster = await Cluster.ConnectAsync("10.70.123.77",
                    ClusterOptions.Default.WithCredentials("Administrator", "NY3gCv4zqG"));
            }
            return _cluster;
        }

        private async Task<IBucket> GetBucketAsync()
        {
            var cluster = await GetClusterAsync();
            return await cluster.BucketAsync("sohoa");
        }

        public async Task<Product?> GetProductByIdAsync(string id)
        {
            var cachedProduct = await _cache.GetStringAsync(id);
            if (!string.IsNullOrEmpty(cachedProduct))
            {
                return JsonSerializer.Deserialize<Product>(cachedProduct);
            }

            try
            {
                var getResult = await _collection.GetAsync(id);

                if (getResult != null)
                {
                    var product = getResult.ContentAs<Product>();
                    await _cache.SetStringAsync(id, JsonSerializer.Serialize(product),
                     new DistributedCacheEntryOptions
                     {
                         AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // TTL 10 minutes
                     });
                    return product;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving product {id}: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Product>> GetProductsAsync()
        {
            var cachedProducts = await _cache.GetStringAsync("products");
            if (!string.IsNullOrEmpty(cachedProducts))
            {
                return JsonSerializer.Deserialize<List<Product>>(cachedProducts) ?? new List<Product>();
            }

            var query = "SELECT id, imageUrl, price, status, name, category FROM sohoa WHERE category = $documentCategory";
            var queryOptions = new QueryOptions().Parameter("documentCategory", "Laptop");

            var queryResult = await _bucket.Cluster.QueryAsync<Product>(query, queryOptions);
            var products = await queryResult.Rows.ToListAsync();

            await _cache.SetStringAsync("products", JsonSerializer.Serialize(products),
             new DistributedCacheEntryOptions
             {
                 AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
             });
            return products;
        }

        public async Task CreateProductAsync(Product product)
        {
            if (string.IsNullOrEmpty(product.Id))
            {
                product.Id = Guid.NewGuid().ToString();
            }

            await _collection.InsertAsync(product.Id, product);
            await UpdateProductListInCache();
        }

        public async Task UpdateProductAsync(string id, Product product)
        {
            await _collection.ReplaceAsync(id, product);
            await UpdateProductListInCache();
        }

        public async Task DeleteProductAsync(string id)
        {
            await _collection.RemoveAsync(id);
            await _cache.RemoveAsync(id);
            await _cache.RemoveAsync("products");
        }

        private async Task UpdateProductListInCache()
        {
            var productsFromDb = await GetProductsAsync();
            await _cache.SetStringAsync("products", JsonSerializer.Serialize(productsFromDb),
                 new DistributedCacheEntryOptions
                 {
                     AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // TTL 10 minutes
                 });
        }
    }
}