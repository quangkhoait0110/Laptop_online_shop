using Couchbase;
using Couchbase.KeyValue;
using Couchbase.Query;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using ProductApi1.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProductApi1.Services
{
    public class OrderService
    {
        private readonly CouchbaseOptions _options;
        private readonly IDistributedCache _cache;
        private ICluster? _cluster;

        public OrderService(IOptions<CouchbaseOptions> options, IDistributedCache cache)
        {
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
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

        public async Task<Order?> GetOrderByIdAsync(string id)
        {
            var cachedOrder = await _cache.GetStringAsync(id);
            if (!string.IsNullOrEmpty(cachedOrder))
            {
                return JsonSerializer.Deserialize<Order>(cachedOrder);
            }

            var bucket = await GetBucketAsync();
            var collection = bucket.DefaultCollection();

            try
            {
                var getResult = await collection.GetAsync(id);

                if (getResult != null)
                {
                    var order = getResult.ContentAs<Order>();
                    await _cache.SetStringAsync(id, JsonSerializer.Serialize(order),
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // TTL 10 minutes
                        });
                    return order;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving order {id}: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Order>> GetOrdersAsync()
        {
            var cachedOrders = await _cache.GetStringAsync("orders");
            if (!string.IsNullOrEmpty(cachedOrders))
            {
                return JsonSerializer.Deserialize<List<Order>>(cachedOrders) ?? new List<Order>();
            }

            var cluster = await GetClusterAsync();
            var query = "SELECT id, email, customerName, orderDetail, totalAmount FROM `sohoa` WHERE customerName = 'Khoa Nguyen';";
            var queryResult = await cluster.QueryAsync<Order>(query);
            var orders = await queryResult.Rows.ToListAsync();

            await _cache.SetStringAsync("orders", JsonSerializer.Serialize(orders),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // TTL 10 minutes
                });
            return orders;
        }

        public async Task CreateOrderAsync(Order order)
        {
            var bucket = await GetBucketAsync();
            var collection = bucket.DefaultCollection();

            await collection.InsertAsync(order.Id, order);

            // Cập nhật cache với danh sách đơn hàng mới
            await UpdateOrderListInCache();
        }

        public async Task UpdateOrderAsync(string id, Order order)
        {
            var bucket = await GetBucketAsync();
            var collection = bucket.DefaultCollection();

            await collection.ReplaceAsync(id, order);

            // Cập nhật cache với danh sách đơn hàng mới
            await UpdateOrderListInCache();
        }

        public async Task DeleteOrderAsync(string id)
        {
            var bucket = await GetBucketAsync();
            var collection = bucket.DefaultCollection();
            await collection.RemoveAsync(id);

            // Xóa đơn hàng khỏi cache
            await _cache.RemoveAsync(id);

            // Xóa cache danh sách đơn hàng
            await _cache.RemoveAsync("orders");
        }

        private async Task UpdateOrderListInCache()
        {
            var ordersFromDb = await GetOrdersAsync();
            await _cache.SetStringAsync("orders", JsonSerializer.Serialize(ordersFromDb),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) // TTL 10 minutes
                });
        }
    }
}
