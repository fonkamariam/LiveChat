using Postgrest.Attributes;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace LiveChat.Models;
using Postgrest.Attributes;
using Postgrest.Models;

[Table("Conversation")]
public class ConversationDto : BaseModel
{
    [PrimaryKey("ConvId", false)] public long ConvId { get; set; } 

    [Column("CreationTime")] public DateTime CreationTime { get; set; } = DateTime.UtcNow;

    [Column("UpdatedTime")] public DateTime UpdatedTime { get; set; } = DateTime.UtcNow;

    [ForeignKey("LastMessage")] public long LastMessage { get; set; }

}

