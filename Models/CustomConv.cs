namespace LiveChat.Models
{
    public class CustomConv
    {
        public string UserName { get; set; } = null;

        public DateTime UpdatedTime { get; set; }

        public string Message { get; set; } = string.Empty;
        
        public long ConversationId { get; set; }
    }
}
