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

        [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("Phone_No")] public string PhoneNo { get; set; } = "";

        [Column("PasswordHash")] public byte[] PasswordHash { get; set; } 

        [Column("PasswordSalt")] public byte[] PasswordSalt { get; set; }

        [Column("Online")] public bool Online { get; set; } = false;

        [Column("Deleted")] public bool Deleted { get; set; } = false;


    }
}