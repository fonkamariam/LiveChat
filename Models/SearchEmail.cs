namespace LiveChat.Models
{
    public class SearchEmail
    {
        public long Id { get; set; }
        
        public string Name { get; set; }

        public string LastName { get; set; }

        public string Email { get; set; }

        public List<string> ProfilePicSearch { get; set; }

        public string Status { get; set; }

        public DateTime LastSeen { get; set; }

        public string Bio { get; set; }

    }
}
