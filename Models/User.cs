using System.ComponentModel.DataAnnotations;
using System.Numerics;



namespace LiveChat.Models
{
    public class User
    {

        public string Name { get; set; } = null; 

        public string Email { get; set; } = null;

        public string Password { get; set; } = null;

        public long? VertificationNo { get; set; }


    }
}