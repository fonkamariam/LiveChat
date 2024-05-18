using System.ComponentModel.DataAnnotations;

namespace LiveChat.Models
{
    public class UpdateGroupSettingModel
    {
        [Required]
        public long ? GroupId { get; set; }
        public string Name { get; set; } = "";

        public string Description { get; set; } = "";
    }
}
