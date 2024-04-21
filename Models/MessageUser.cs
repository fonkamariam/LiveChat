namespace LiveChat.Models
{
    public class MessageUser
    {
        
        public string ChatType { get; set; } = "";

        public string Content { get; set; } = "";
        
        public long RecpientId { get; set; } 
        
        public string MessageType { get; set; } = "";

    }
}
