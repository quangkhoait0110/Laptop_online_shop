using System.ComponentModel.DataAnnotations;

namespace ProductApi1.Models
{
    public class RegisterModel
    {
        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string? Username { get; set; }
        [Required]
        [EmailAddress]
        public string? Email { get; set; }
        [Required]
        [StringLength(100, MinimumLength =6)]
        public  string? Password { get; set; }
        [Required]
        [Compare("Password")]
        public string? ConfirmPassword { get; set;}
    }
}