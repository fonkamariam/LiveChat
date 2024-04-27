using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace LiveChat.Models;
using Postgrest.Attributes;
using Postgrest.Models;


[Table("VerificationRegistry")]
public class RegisterVertifyDto : BaseModel
    {
    [PrimaryKey("Id", false)] public long Id { get; set; }

    [Column("Email")] public string Email { get; set; }

    [Column("VertficationNo")]public long? VertficationNo { get; set; }
    
    [Column("VReg_Expiry")]public DateTime VReg_Expiry { get; set; } 


    }