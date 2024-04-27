using Postgrest.Attributes;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace LiveChat.Models;
using Postgrest.Attributes;
using Postgrest.Models;

[Table("GroupMembers")]
public class GroupMemberDto : BaseModel
{
    [PrimaryKey("MemberId", false)] public long MemberId { get; set; }

    [Column("JoinedTime")] public DateTime JoinedTime { get; set; } = DateTime.UtcNow;

    [ForeignKey("GroupId")] public long GroupId { get; set; } 

    [Column("Role")] public string Role { get; set; } = "";
    [Column("Deleted")] public bool? Deleted { get; set; }

    [ForeignKey("UserId")] public long UserId { get; set; }

}