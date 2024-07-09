namespace LiveChat.Models
{
	public class PayLoad
	{

        public string type { get; set; }
        public string table { get; set; }
        public Record record { get; set; }
        public string schema { get; set; }
        public Record old_record { get; set; }

    }
    public class Record
    {
        public long Id { get; set; }
        public long ConvId { get; set; }
        public string Status { get; set; }
        public bool New { get; set; }
        public string Content { get; set; }
        public bool Deleted { get; set; }
        public string ChatType { get; set; } 
        public int SenderId { get; set; }
        public DateTime TimeStamp { get; set; }
        public int RecpientId { get; set; }
        public string MessageType { get; set; }
        public long Deleteer { get; set; }
        public bool isImage { get; set; }
        public bool isAudio { get; set; }
        public string Email { get; set; }
        public string Bio {  get; set; }
        public string OnlineStatus { get; set; }
        public DateTime LastSeen { get; set; }
        public string ProfilePic { get; set; }
        public bool Edited { get; set; }
        public long Reply { get; set; }

    }

}
