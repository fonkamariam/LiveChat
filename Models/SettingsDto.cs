using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace LiveChat.Models;
using Postgrest.Attributes;
using Postgrest.Models;


[Table("Settings")]
public class SettingsDto : BaseModel
{
    [PrimaryKey("Id", false)] public long Id { get; set; }

        [Column("Notification")] public JsonContent Notification { get; set; } 

        [Column("Presence")] public JsonContent Presence { get; set; } = null;

        [Column("Apperance")] public JsonContent Apperance { get; set; }

        [ForeignKey("UserId")] public long UserId { get; set; }

    }
