using Postgrest.Attributes;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace LiveChat.Models;
using Postgrest.Attributes;
using Postgrest.Models;

[Table("GroupConversation")]
public class GroupConversationDto : BaseModel
{
    [PrimaryKey("Id", false)] public long Id { get; set; }

    [Column("Created_at")] public DateTime Created_at { get; set; } = DateTime.UtcNow;

    [Column("Updated_time")] public DateTime Updated_time { get; set; } = DateTime.UtcNow;

    [ForeignKey("LastGroupMessage")] public long LastGroupMessage { get; set; }

}