namespace LiveChat.Models
{
    public class UserCustomModel
    {
        public long Id { get; set; }

        public string Email { get; set; }
        
        public byte[] PasswordHash { get; set; }

        public byte[] PasswordSalt { get; set; }

    }
}
