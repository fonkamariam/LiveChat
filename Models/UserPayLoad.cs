namespace LiveChat.Models
{
    public class UserPayLoad
    {

        public string type { get; set; }
        public string table { get; set; }
        public RecordUser record { get; set; }
        public string schema { get; set; }
        public RecordUser old_record { get; set; }

    }
    public class RecordUser
    {
        public long UserId { get; set; }
        public long ProfileId { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public bool Deleted { get; set; }
        public string Bio { get; set; }
        public string ProfilePic { get; set; }
        
    }

}
