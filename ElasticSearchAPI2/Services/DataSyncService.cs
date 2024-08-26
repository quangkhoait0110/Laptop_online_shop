using Couchbase;
using Couchbase.KeyValue;
using Couchbase.Query;
using Microsoft.Extensions.Hosting;
using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElasticSearchAPI2.Models;

namespace ElasticSearchAPI2.Services
{
    public class DataSyncService : BackgroundService
    {
        private readonly ICluster _couchbaseCluster;
        private readonly ElasticClient _elasticClient;
        private const string IndexName = "products";

        public DataSyncService(ElasticClient elasticClient)
        {
            _elasticClient = elasticClient;
            _couchbaseCluster = ConnectToCouchbase().Result;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await SyncData();
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private async Task<ICluster> ConnectToCouchbase()
        {
            var options = new ClusterOptions
            {
                ConnectionString = "couchbase://10.70.123.77",
                UserName = "Administrator",
                Password = "NY3gCv4zqG"
            };
            return await Couchbase.Cluster.ConnectAsync(options);
        }

       private async Task SyncData()
{
    var bucket = await _couchbaseCluster.BucketAsync("sohoa");
    var collection = bucket.DefaultCollection();

    var elasticsearchIds = await GetAllElasticsearchIds();


    var query = "SELECT id, imageUrl, price, status, name, category FROM `sohoa` WHERE category = $documentCategory";
    var queryOptions = new QueryOptions().Parameter("documentCategory", "Laptop");
    var result = await _couchbaseCluster.QueryAsync<Product>(query, queryOptions);

    var couchbaseIds = new HashSet<string>();

    await foreach (var doc in result.Rows)
    {
        couchbaseIds.Add(doc.Id);
        await _elasticClient.IndexDocumentAsync(doc);
    }

    var idsToDelete = elasticsearchIds.Except(couchbaseIds).ToList();
    if (idsToDelete.Any())
    {
        var bulkDescriptor = new BulkDescriptor();
        foreach (var id in idsToDelete)
        {
            bulkDescriptor.Delete<Product>(d => d.Index(IndexName).Id(id));
        }

        var deleteResponse = await _elasticClient.BulkAsync(bulkDescriptor);
        if (!deleteResponse.IsValid)
        {
            Console.WriteLine($"Error deleting documents: {deleteResponse.DebugInformation}");
        }
    }
}

        private async Task<IEnumerable<string>> GetAllElasticsearchIds()
        {
            var searchResponse = await _elasticClient.SearchAsync<Product>(s => s
                .Index(IndexName)
                .Size(10000)
                .Source(false)
                .Query(q => q.MatchAll())
            );

            return searchResponse.Hits.Select(hit => hit.Id);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await _couchbaseCluster.DisposeAsync();
            await base.StopAsync(cancellationToken);
        }
    }
}