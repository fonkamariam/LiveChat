using System.ComponentModel.DataAnnotations;

namespace LiveChat.Models
{
    public class GroupUser
    {

        [Required]
        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

    }
}
