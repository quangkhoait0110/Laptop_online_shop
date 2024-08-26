using Confluent.Kafka;
using EllipticCurve;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using ProductApi1.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProductApi1.Services
{
    public class KafkaConsumerService : IHostedService, IDisposable
    {
        private readonly IConsumer<string, string> _consumer;
        private readonly CouchbaseService _couchbaseService;
        private readonly IDistributedCache _cache;
        private readonly EmailService _emailService;
        private readonly OrderService _orderService;

        public KafkaConsumerService(CouchbaseService couchbaseService, IDistributedCache cache, EmailService emailService, OrderService orderService)
        {
            _couchbaseService = couchbaseService;
            _cache = cache;
            _emailService = emailService;
            _orderService = orderService;

            var config = new ConsumerConfig
            {
                GroupId = "product-order-consumers",
                BootstrapServers = "10.70.123.76:31633",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            _consumer = new ConsumerBuilder<string, string>(config).Build();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Task.Run(() => ConsumeMessages(cancellationToken), cancellationToken);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _consumer.Close();
            await Task.CompletedTask;
        }

        private async Task ConsumeMessages(CancellationToken cancellationToken)
        {
            _consumer.Subscribe(new List<string>
            {
                "products-create-request-topic",
                "products-update-request-topic",
                "products-delete-request-topic",
                "orders-create-request-topic",
                "orders-update-request-topic",
                "orders-delete-request-topic"
            });

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = _consumer.Consume(cancellationToken);

                        if (consumeResult != null)
                        {
                            switch (consumeResult.Topic)
                            {
                                case "products-create-request-topic":
                                    await HandleProductCreateRequest(consumeResult.Message.Value);
                                    break;
                                case "products-update-request-topic":
                                    await HandleProductUpdateRequest(consumeResult.Message.Key, consumeResult.Message.Value);
                                    break;
                                case "products-delete-request-topic":
                                    await HandleProductDeleteRequest(consumeResult.Message.Key);
                                    break;
                                case "orders-create-request-topic":
                                    await HandleOrderCreateRequest(consumeResult.Message.Value);
                                    break;
                                case "orders-update-request-topic":
                                    await HandleOrderUpdateRequest(consumeResult.Message.Key, consumeResult.Message.Value);
                                    break;
                                case "orders-delete-request-topic":
                                    await HandleOrderDeleteRequest(consumeResult.Message.Key);
                                    break;
                                default:
                                    Console.WriteLine($"Received message from unexpected topic: {consumeResult.Topic}");
                                    break;
                            }

                            // Commit offset manually after processing each message
                            _consumer.Commit(consumeResult);
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        Console.WriteLine($"Error occurred while consuming message: {ex.Error.Reason}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error occurred: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {}
            finally
            {
                _consumer.Close();
            }
        }

        private async Task HandleProductCreateRequest(string messageValue)
        {
            var product = JsonConvert.DeserializeObject<Product>(messageValue);
            if (product != null)
            {
                var existingProduct = await _couchbaseService.GetProductByIdAsync(product.Id);
                if (existingProduct == null)
                {
                    await _couchbaseService.CreateProductAsync(product);

                    // Update product in cache
                    await UpdateProductInCache(product.Id, product);

                    // Update GetAll cache
                    await UpdateAllProductsCache(product);

                    // Send email notification
                    var subject = "New Product Created";
                    var body = $"A new product has been created:\n\nID: {product.Id}\nName: {product.Name}\nImageUrl: {product.ImageUrl}";
                    await _emailService.SendEmailAsync("quangkhoadt1107@gmail.com", subject, body);

                    Console.WriteLine($"Product created and email notification sent: {messageValue}");
                }
                else
                {
                    Console.WriteLine($"Product with ID {product.Id} already exists.");
                }
            }
            else
            {
                Console.WriteLine("Received invalid product data.");
            }
        }


        private async Task UpdateAllProductsCache(Product newProduct)
        {
            var cachedProductsJson = await _cache.GetStringAsync("products");
            if (cachedProductsJson != null)
            {
                var cachedProducts = JsonConvert.DeserializeObject<List<Product>>(cachedProductsJson);
                if (cachedProducts != null)
                {
                    cachedProducts.Add(newProduct);
                    var updatedProductsJson = JsonConvert.SerializeObject(cachedProducts);
                    await _cache.SetStringAsync("products", updatedProductsJson, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                    });
                }
                else
                {
                    // If deserialization failed, create a new list with only the new product
                    var newProductsList = new List<Product> { newProduct };
                    var newProductsJson = JsonConvert.SerializeObject(newProductsList);
                    await _cache.SetStringAsync("all_products", newProductsJson, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                    });
                }
            }
            else
            {
                // If the cache doesn't exist, create it with the new product
                var newProductsList = new List<Product> { newProduct };
                var newProductsJson = JsonConvert.SerializeObject(newProductsList);
                await _cache.SetStringAsync("all_products", newProductsJson, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                });
            }
        }

        private async Task HandleProductUpdateRequest(string productId, string messageValue)
        {
            var product = JsonConvert.DeserializeObject<Product>(messageValue);
            if (product != null)
            {
                await _couchbaseService.UpdateProductAsync(productId, product);

                // Update product in cache
                await UpdateProductInCache(productId, product);

                Console.WriteLine($"Product updated: {messageValue}");
            }
            else
            {
                Console.WriteLine($"Received invalid product data for ID: {productId}");
            }
        }

        private async Task HandleProductDeleteRequest(string productId)
        {
            if (!string.IsNullOrEmpty(productId))
            {
                await _couchbaseService.DeleteProductAsync(productId);

                // Remove product from cache
                await _cache.RemoveAsync(productId);

                // Remove all products cache
                await _cache.RemoveAsync("products");

                Console.WriteLine($"Product deleted: {productId}");
            }
            else
            {
                Console.WriteLine("Received invalid product ID.");
            }
        }

        private async Task HandleOrderCreateRequest(string messageValue)
        {
            var order = JsonConvert.DeserializeObject<Order>(messageValue);
            if (order != null)
            {
                await _orderService.CreateOrderAsync(order);
                await UpdateOrderInCache(order.Id, order);

                await UpdateAllOrdersCache(order);
                // Send email notification
                var subject = "New Order Created";
                var body = $"A new order has been created:\n\nOrder ID: {order.Id}\nCustomer Name: {order.customerName}\nOrder Detail: {order.orderDetail}\nTotal: {order.totalAmount}";
                await _emailService.SendEmailAsync(order.email, subject, body);

                Console.WriteLine($"Order created and email notification sent: {messageValue}");
            }
            else
            {
                Console.WriteLine("Received invalid order data.");
            }
        }

        public async Task UpdateAllOrdersCache(Order newOrder)
        {
            var cachedOrdersJson = await _cache.GetStringAsync("orders");
            if (cachedOrdersJson != null)
            {
                var cachedOrders = JsonConvert.DeserializeObject<List<Order>>(cachedOrdersJson);
                if (cachedOrders != null)
                {
                    cachedOrders.Add(newOrder);
                    var updatedOrdersJson = JsonConvert.SerializeObject(cachedOrders);
                    await _cache.SetStringAsync("orders", updatedOrdersJson, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                    });
                }
                else
                {
                    // If deserialization failed, create a new list with only the new product
                    var newOrdersList = new List<Order> { newOrder };
                    var newOrdersJson = JsonConvert.SerializeObject(newOrdersList);
                    await _cache.SetStringAsync("all_orders", newOrdersJson, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                    });
                }
            }
            else
            {
                // If the cache doesn't exist, create it with the new order
                var newOrdersList = new List<Order> { newOrder };
                var newOrdersJson = JsonConvert.SerializeObject(newOrdersList);
                await _cache.SetStringAsync("all_orders", newOrdersJson, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                });
            }
        }

        private async Task UpdateOrderInCache(string orderId, Order order)
        {
            var orderJson = JsonConvert.SerializeObject(order);
            await _cache.SetStringAsync(orderId, orderJson);
        }

        private async Task HandleOrderUpdateRequest(string orderId, string messageValue)
        {
            var order = JsonConvert.DeserializeObject<Order>(messageValue);
            if (order != null)
            {
                await _orderService.UpdateOrderAsync(orderId, order);

                Console.WriteLine($"Order updated: {messageValue}");
            }
            else
            {
                Console.WriteLine($"Received invalid order data for ID: {orderId}");
            }
        }

        private async Task HandleOrderDeleteRequest(string orderId)
        {
            if (!string.IsNullOrEmpty(orderId))
            {
                await _orderService.DeleteOrderAsync(orderId);

                Console.WriteLine($"Order deleted: {orderId}");
            }
            else
            {
                Console.WriteLine("Received invalid order ID.");
            }
        }

        private async Task UpdateProductInCache(string productId, Product product)
        {
            var productJson = JsonConvert.SerializeObject(product);
            await _cache.SetStringAsync(productId, productJson);
        }

        public void Dispose()
        {
            _consumer.Dispose();
        }
    }
}
