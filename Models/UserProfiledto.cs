using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System.Text.Json;

namespace LiveChat.Models;

using Newtonsoft.Json;
using Postgrest.Attributes;
using Postgrest.Models;
using System.Text.Json.Serialization;

[Table("UserProfile")]
public class UserProfiledto:BaseModel
    {

    [PrimaryKey("ProfileId", false)] public long Id { get; set; }

    [ForeignKey("UserId")] public long UserId { get; set; }

    [Required] [Column("Name")] public string? Name { get; set; } = null;

    [Column("UserName")] public string? UserName { get; set; } = null;

    [Column("Avatar")] public string? Avatar { get; set; }

    [Column("Bio")] public string? Bio { get; set; }

    [Column("LastName")] public string? LastName { get; set; }

    [Column("Deleted")] public bool Deleted { get; set; } = false;

    [Column("Status")] public string Status { get; set; } = "false";
    
    [Column("LastSeen")] public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    [Column("ProfilePic")] public string ProfilePic { get; set; }

    

    }

