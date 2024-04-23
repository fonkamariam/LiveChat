using Postgrest.Attributes;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace LiveChat.Models;
using Postgrest.Attributes;
using Postgrest.Models;

[Table("GroupMessage")]
public class GroupMessageDto : BaseModel
{
    [PrimaryKey("Id", false)] public long Id { get; set; }

    [Column("Created_at")] public DateTime Created_at { get; set; } = DateTime.UtcNow;
    [Column("Content")] public string Content { get; set; } = "";

    [Column("Type")] public string Type { get; set; } = "";

    [ForeignKey("RecGroupId")] public long RecGroupId { get; set; }

    [ForeignKey("MemberSenderId")] public long MemberSenderId { get; set; }

}