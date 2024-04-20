using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace LiveChat.Models;
using Postgrest.Attributes;
using Postgrest.Models;


[Table("Contacts")]
public class ContactDto : BaseModel
{

    [PrimaryKey("ContactId", false)] public long ContactId { get; set; }

    [ForeignKey("ContacterId")] public long ContacterId { get; set; }

    [ForeignKey("ContacteeId")] public long ContacteeId { get; set; }

    [Column("Blocked")] public bool Blocked { get; set; } = false;

    [Column("Block")] public bool Block { get; set; } = false;

    [Column("created_at")] public DateTime created_at { get; set; } = DateTime.UtcNow;

}