using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Reflection.Metadata;
using Postgrest.Attributes;
using Postgrest.Models;


namespace LiveChat.Models
{
    [Table("Users")]
    public class Userdto : BaseModel
    {
        [PrimaryKey("Id",false)] public long Id { get; set; }

        [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [Required]
        [Column("Phone_No")] public string? PhoneNo { get; set; } = null;

        [Required]
        [Column("Email")] public string? Email { get; set; }

        [Required]
        [Column("PasswordHash")] public byte[]? PasswordHash { get; set; } 

        [Column("PasswordSalt")] public byte[]? PasswordSalt { get; set; }

        [Column("Online")] public bool Online { get; set; } = false;

        [Column("Deleted")] public bool Deleted { get; set; } = false;

        [Column("Last_Seen")] public DateTime Last_Seen { get; set; } = DateTime.UtcNow;

        [Column("Refresh_Token")] public string? Refresh_Token { get; set; } = null;
        [Column("Token_Created")] public DateTime Token_Created { get; set; } = DateTime.UtcNow;
        [Column("Token_Expiry")] public DateTime Token_Expiry { get; set; } = DateTime.UtcNow;

        [Column("V_Number_Value")] public long? V_Number_Value { get; set; } = 0;
        [Column("V_Number_Created_At")] public DateTime V_Number_Created_At { get; set; } = DateTime.UtcNow;
        [Column("V_Number_Expiry")] public DateTime V_Number_Expiry { get; set; } = DateTime.UtcNow;
        
    }
}