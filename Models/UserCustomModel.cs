namespace LiveChat.Models
{
    public class UserCustomModel
    {
        public long Id { get; set; }

        public string PhoneNo { get; set; }

        public byte[] PasswordHash { get; set; }

        public byte[] PasswordSalt { get; set; }

        public bool Online { get; set; }

        public bool Deleted { get; set; }
    }
}
