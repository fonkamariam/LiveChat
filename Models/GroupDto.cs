using Postgrest.Attributes;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace LiveChat.Models;
using Postgrest.Attributes;
using Postgrest.Models;

[Table("Group")]
    public class GroupDto:BaseModel
    {
        [PrimaryKey("GroupId", false)] public long GroupId { get; set; }

        [Column("created_at")] public DateTime Created_at { get; set; } = DateTime.UtcNow;

        [Column("Name")] public string Name { get; set; } = "" ;

        [Column("Description")] public string Description { get; set; } = "";

        [Column("Deleted")] public bool? Deleted { get; set; }

        [ForeignKey("CreatorId")] public long CreatorId { get; set; }
        [ForeignKey("G_CoversationId")] public long G_CoversationId { get; set; }


}

