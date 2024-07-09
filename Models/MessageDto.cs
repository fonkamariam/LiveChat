﻿using Postgrest.Attributes;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace LiveChat.Models;
using Postgrest.Attributes;
using Postgrest.Models;

[Table("Messages")]
public class MessageDto : BaseModel
    {
        [PrimaryKey("id", false)] public long Id { get; set; }

        [Column("TimeStamp")] public DateTime TimeStamp { get; set; } = DateTime.UtcNow;

        [Column("Content")] public string Content { get; set; } = "";

        [Column("Status")] public string? Status { get; set; } = "";

        [Column("MessageType")] public string MessageType { get; set; } = "";
        
        [Column("Edited")] public bool Edited { get; set; } = false;

        [Column("IsAudio")] public bool IsAudio { get; set; }

        [Column("IsImage")] public bool IsImage { get; set; }

        [Column("New")] public bool New {  get; set; } = true;

        [Column("Reply")] public long Reply { get; set; }    
 
        [ForeignKey("SenderId")] public long SenderId { get; set; }
        
        [ForeignKey("RecpientId")] public long RecpientId { get; set; }

        [Column("Deleted")] public bool Deleted { get; set; }

        [ForeignKey("ConvId")] public long ConvId { get; set; }

        [ForeignKey("Deleteer")] public long Deleteer { get; set; }

        



}

