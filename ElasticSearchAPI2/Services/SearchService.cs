// Services/SearchService.cs
using ElasticSearchAPI2.Models;
using Nest;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ElasticSearchAPI2.Services
{
    public class SearchService
    {
        private readonly ElasticClient _elasticClient;

        public SearchService(ElasticClient elasticClient)
        {
            _elasticClient = elasticClient;
        }

        public async Task<Product> GetDocumentByIdAsync(string id)
        {
            var getResponse = await _elasticClient.GetAsync<Product>(id, idx => idx.Index("products"));
            return getResponse.Found ? getResponse.Source : null!;
        }

        public async Task<IEnumerable<Product>> SearchAsync(string query)
        {
            var searchResponse = await _elasticClient.SearchAsync<Product>(s => s
                .Index("products")
                .Query(q => q
                    .MultiMatch(m => m
                        .Fields(f => f
                            .Field(ff => ff.Name)
                            .Field(ff => ff.ImageUrl)
                            .Field(ff => ff.Category)
                            .Field(ff => ff.Status)
                        )
                        .Query(query)
                    )
                )
            );

            return searchResponse.IsValid ? searchResponse.Documents : null!;
        }
    }
}