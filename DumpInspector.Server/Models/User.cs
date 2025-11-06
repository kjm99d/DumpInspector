using System.Text.Json.Serialization;

namespace DumpInspector.Server.Models
{
    using System.ComponentModel.DataAnnotations;

    public class User
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Username { get; set; } = default!;
        // Stored password hash (base64)
        public string PasswordHash { get; set; } = default!;
        // Salt used for hash (base64)
        public string Salt { get; set; } = default!;
        public bool IsAdmin { get; set; }
        public string? Email { get; set; }
    }
}
