namespace ElasticSearchAPI2.Models
{
    public class Product
    {

        public required string Id { get; set; }
        public required string Name { get; set; }

        public required string ImageUrl { get; set; }

        public required string Category { get; set; }

        public decimal Price { get; set; }

        public required string Status { get; set; }

    }
}