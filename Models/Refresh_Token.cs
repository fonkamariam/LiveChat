namespace LiveChat.Models
{
    public class Refresh_Token
    {
        public string Token { get; set; } = string.Empty;

        public DateTime Created { get; set; }= DateTime.UtcNow;

        public DateTime Expires { get; set; }
    }
}