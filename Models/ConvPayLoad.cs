namespace LiveChat.Models
{
    public class ConvPayLoad
    {

        public string type { get; set; }
        public string table { get; set; }
        public string schema { get; set; }
        public RecordConv old_record { get; set; }

    }
    public class RecordConv
    {
        public long ConvId { get; set; }
        public long LastMessage { get; set; }
        
    }

}
