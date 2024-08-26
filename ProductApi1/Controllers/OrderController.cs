using Microsoft.AspNetCore.Mvc;
using ProductApi1.Models;
using ProductApi1.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

namespace ProductApi1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly IDistributedCache _cache;
        private readonly KafkaProducerService _kafkaProducerService;
        private readonly OrderService _orderService;

        public OrdersController(IDistributedCache cache, KafkaProducerService kafkaProducerService, OrderService orderService)
        {
            _cache = cache;
            _kafkaProducerService = kafkaProducerService;
            _orderService = orderService;
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

        [HttpPost]
        public async Task<ActionResult<Order>> CreateOrder([FromBody]Order order)
        {
            if (string.IsNullOrEmpty(order.Id))
            {
                order.Id = Guid.NewGuid().ToString();
            }

            await _kafkaProducerService.SendMessageAsync("orders-create-request-topic", order.Id, order);
            return Ok(order);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Order>> GetOrder(string id)
        {
            var cachedOrder = await GetCachedDataAsync<Order>(id);
            if (cachedOrder != null)
            {
                return Ok(cachedOrder);
            }

            var orderFromDb = await _orderService.GetOrderByIdAsync(id);
            if (orderFromDb == null)
            {
                return NotFound("Order not found with ID: " + id);
            }

            await CacheDataAsync(id, orderFromDb, TimeSpan.FromMinutes(10));

            return Ok(orderFromDb);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
        {
            var orderList = await GetCachedDataAsync<List<Order>>("orders");

            if (orderList != null)
            {
                return Ok(orderList);
            }

            var ordersFromDb = await _orderService.GetOrdersAsync();

            if (ordersFromDb != null && ordersFromDb.Any())
            {
                await CacheDataAsync("orders", ordersFromDb, TimeSpan.FromMinutes(10));
                return Ok(ordersFromDb);
            }

            return NotFound("No orders found");
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOrder(string id, Order order)
        {
            if (id != order.Id)
            {
                return BadRequest("Order ID mismatch");
            }

            await _kafkaProducerService.SendMessageAsync("orders-update-request-topic", id, order);
            await CacheDataAsync(id, order, TimeSpan.FromMinutes(10));

            var orderList = await GetCachedDataAsync<List<Order>>("orders");
            if (orderList != null)
            {
                var orderToUpdate = orderList.FirstOrDefault(p => p.Id == id);
                if (orderToUpdate != null)
                {
                    orderList.Remove(orderToUpdate);
                    orderList.Add(order);
                    await CacheDataAsync("orders", orderList, TimeSpan.FromMinutes(10));
                }
            }

            return Accepted(new { Message = "Order updated successfully" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(string id)
        {
            var existingOrder = await _orderService.GetOrderByIdAsync(id);
            if (existingOrder == null)
            {
                return NotFound($"Order not found with ID: {id}");
            }

            await _kafkaProducerService.SendMessageAsync("orders-delete-request-topic", id, "deleted");
            await _cache.RemoveAsync(id);

            var orderList = await GetCachedDataAsync<List<Order>>("orders");
            if (orderList != null)
            {
                var orderToRemove = orderList.FirstOrDefault(p => p.Id == id);
                if (orderToRemove != null)
                {
                    orderList.Remove(orderToRemove);
                    await CacheDataAsync("orders", orderList, TimeSpan.FromMinutes(10));
                }
            }

            return Accepted(new { Message = $"Deleted successfully order id: {id}" });
        }
    }
}