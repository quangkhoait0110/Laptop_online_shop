using System.ComponentModel.DataAnnotations;

namespace ProductApi1.Models
{
    public class Product
    {

        public required string Id { get; set; }
        [Required]
        public  required string Name { get; set; }

        public required string ImageUrl { get; set; }

        public required string Category { get; set; }

        [Range(0,9999)]
        public required decimal Price { get; set; }
        

        public required string Status { get; set; }

    }
}