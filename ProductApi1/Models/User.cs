using System;

namespace ProductApi1.Models
{
    public class User
    {
        public string? Id { get; set; }
        public required string Username { get; set; }
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }
    }
}