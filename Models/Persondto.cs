using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Reflection.Metadata;



namespace LiveChat.Models
{
    public class Persondto
    {


        public int phoneNo { get; set; }

        public byte[] passwordHash { get; set; } 
        public byte[] passwordSalt { get; set; }

    }
}