namespace ProductApi1.Models
{
    public class CouchbaseOptions
    {
        public required string Servers { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string Bucket { get; set; }
    }
}
