// Controllers/ProductsController.cs
using Microsoft.AspNetCore.Mvc;
using ProductApi1.Services;
using ProductApi1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;

namespace ProductApi1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IDistributedCache _cache;
        private readonly KafkaProducerService _kafkaProducerService;
        private readonly CouchbaseService _couchbaseService;
        private readonly MinioService _minioService;

        public ProductsController(MinioService minioService, IDistributedCache cache, KafkaProducerService kafkaProducerService, CouchbaseService couchbaseService)
        {
            _cache = cache;
            _kafkaProducerService = kafkaProducerService;
            _couchbaseService = couchbaseService;
            _minioService = minioService;
        }

        private async Task CacheDataAsync<T>(string key, T data, TimeSpan expiration)
        {
            var serializedData = JsonConvert.SerializeObject(data);
            await _cache.SetStringAsync(key, serializedData, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            });
        }

        private async Task<T?> GetCachedDataAsync<T>(string key) where T : class
        {
            var cachedData = await _cache.GetStringAsync(key);
            return string.IsNullOrEmpty(cachedData) ? default : JsonConvert.DeserializeObject<T>(cachedData);
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<Product>> CreateProduct(Product product)
        {
            var existingProduct = await _couchbaseService.GetProductByIdAsync(product.Id);
            if (existingProduct != null)
            {
                return Conflict($"Product with id : {product.Id} already exists.");
            }

            await _kafkaProducerService.SendMessageAsync("products-create-request-topic", product.Id, product);
            return Ok(product);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            var productList = await GetCachedDataAsync<List<Product>>("products");

            if (productList != null)
            {
                return Ok(productList);
            }

            var productsFromDb = await _couchbaseService.GetProductsAsync();

            if (productsFromDb != null && productsFromDb.Any())
            {
                await CacheDataAsync("products", productsFromDb, TimeSpan.FromMinutes(10));
                return Ok(productsFromDb);
            }

            return NotFound("No products found");
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(string id)
        {
            var product = await GetCachedDataAsync<Product>(id);

            if (product != null)
            {
                return Ok(product);
            }

            var productFromDb = await _couchbaseService.GetProductByIdAsync(id);
            if (productFromDb == null)
            {
                return NotFound("Product not found with ID: " + id);
            }

            await CacheDataAsync(id, productFromDb, TimeSpan.FromMinutes(10));

            return Ok(productFromDb);
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(string id, Product product)
        {
            if (id != product.Id)
            {
                return NotFound("Product not found with ID: " + id);
            }

            await _kafkaProducerService.SendMessageAsync("products-update-request-topic", id, product);

            await CacheDataAsync(id, product, TimeSpan.FromMinutes(10));

            var productList = await GetCachedDataAsync<List<Product>>("products");
            if (productList != null)
            {
                var productToUpdate = productList.FirstOrDefault(p => p.Id == id);
                if (productToUpdate != null)
                {
                    productList.Remove(productToUpdate);
                    productList.Add(product);
                    await CacheDataAsync("products", productList, TimeSpan.FromMinutes(10));
                }
            }

            return Accepted(new { Message = "Updated successfully" });
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(string id)
        {
            var existingProduct = await _couchbaseService.GetProductByIdAsync(id);
            if (existingProduct == null)
            {
                return NotFound($"Product not found with ID: {id}");
            }

            await _kafkaProducerService.SendMessageAsync("products-delete-request-topic", id, "deleted");

            await _cache.RemoveAsync(id);

            var productList = await GetCachedDataAsync<List<Product>>("products");
            if (productList != null)
            {
                var productToRemove = productList.FirstOrDefault(p => p.Id == id);
                if (productToRemove != null)
                {
                    productList.Remove(productToRemove);
                    await CacheDataAsync("products", productList, TimeSpan.FromMinutes(10));
                }
            }

            return Accepted(new { Message = $"Deleted successfully product id: {id}" });
        }

        [HttpGet("presigned-url")]
        public async Task<IActionResult> GetPresignedUrlForUpload(string objectName)
        {
            var presignedUrl = await _minioService.GeneratePresignedUrlForUpload(objectName);
            return Ok(new { PresignedUrl = presignedUrl, ObjectName = objectName });
        }

        [HttpGet("get-presigned-url")]
        public async Task<IActionResult> GetPresignedUrlForReading(string objectName)
        {
            var presignedUrl = await _minioService.GeneratePresignedUrlForReading(objectName);
            return Ok(new { PresignedUrl = presignedUrl, ObjectName = objectName });
        }
    }
}