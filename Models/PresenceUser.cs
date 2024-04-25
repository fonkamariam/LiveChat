namespace LiveChat.Models
{
    public class PresenceInfo
    {
        public string Status { get; set; }
        public string LastSeen { get; set; }
    } 
    public class PresenceUser
    {
        public PresenceInfo Presence { get; set; }
    }


}
