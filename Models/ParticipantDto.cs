using Postgrest.Attributes;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace LiveChat.Models;
using Postgrest.Attributes;
using Postgrest.Models;

[Table("Participant")]
public class ParticipantDto : BaseModel
{
    [PrimaryKey("ParticipantId", false)] public long ParticipantId { get; set; }

    [ForeignKey("UserId")] public long UserId { get; set; }

    [ForeignKey("ConversationId")] public long ConversationId { get; set; }

}


