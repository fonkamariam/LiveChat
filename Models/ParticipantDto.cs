using Postgrest.Attributes;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveChat.Models;

using Postgrest.Models;
using System.ComponentModel.DataAnnotations.Schema;

[Table("Participant")]
public class ParticipantDto : BaseModel
{
    [PrimaryKey("ParticipantId", false)] public long ParticipantId { get; set; }

    [ForeignKey("UserId")] public long UserId { get; set; }

    [ForeignKey("ConversationId")] public long ConversationId { get; set; }

}


