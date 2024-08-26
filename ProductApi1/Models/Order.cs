
namespace ProductApi1.Models
{
    public class Order
    {
        public required string Id { get; set;}
        public required string email { get; set; }
        public required string customerName {get; set;}
        public required string orderDetail { get; set;}
        public decimal totalAmount { get; set; }
    }
}
