namespace LiveChat.Models
{
    public class CustomConv
    {
        public string UserName { get; set; } = null;
        
        public string LastName { get; set; } = null;

        public DateTime UpdatedTime { get; set; }

        public string Message { get; set; } = string.Empty;
        public bool Seen { get; set; }
        
        public long UserId { get; set; }

        public long ConvId { get; set; }

        public long MessageId { get; set; }
        public long MessageSender { get; set; }

        public long NotificationCount { get; set; }

        public string Status { get; set; }

        public DateTime LastSeen { get; set; }

        public string Bio { get; set; }

        public string Email { get; set; }

        public bool IsAudio { get; set; }
        public bool IsImage { get; set; }

        public List<string> ProfilePicConv { get; set;}

        public bool Deleted { get; set; }
    }
}
