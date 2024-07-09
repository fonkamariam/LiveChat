namespace LiveChat.Models
{
    public class MessageUser
    {
        
        
        public string Content { get; set; } = "";
        
        public long RecpientId { get; set; } 
        
        public string MessageType { get; set; } = "";

        public bool IsAudio { get; set; }

        public bool IsImage { get; set; }
        
        public long ConversationId {get; set;}

        public long Reply { get; set; }

    }
}
