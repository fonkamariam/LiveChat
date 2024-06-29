using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Newtonsoft.Json;
using Postgrest.Attributes;
using Postgrest.Models;

namespace LiveChat.Models
{
    [Postgrest.Attributes.Table("Users")]
    public class Userdto : BaseModel
    {
        [Postgrest.Attributes.PrimaryKey("Id", false)] public long Id { get; set; }

        [Postgrest.Attributes.Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Postgrest.Attributes.Column("Email")] public string? Email { get; set; }

        [Postgrest.Attributes.Column("PasswordHash")] public byte[]? PasswordHash { get; set; }

        [Postgrest.Attributes.Column("PasswordSalt")] public byte[]? PasswordSalt { get; set; }

        [Postgrest.Attributes.Column("Deleted")] public bool Deleted { get; set; } = false;

        [Postgrest.Attributes.Column("Refresh_Token")] public string? Refresh_Token { get; set; } = null;

        [Postgrest.Attributes.Column("Token_Created")] public DateTime Token_Created { get; set; } = DateTime.UtcNow;

        [Postgrest.Attributes.Column("Token_Expiry")] public DateTime Token_Expiry { get; set; } = DateTime.UtcNow;

        [Postgrest.Attributes.Column("V_Number_Value")] public long? V_Number_Value { get; set; } = 0;

        [Postgrest.Attributes.Column("V_Number_Created_At")] public DateTime V_Number_Created_At { get; set; } = DateTime.UtcNow;

        [Postgrest.Attributes.Column("V_Number_Expiry")] public DateTime V_Number_Expiry { get; set; } = DateTime.UtcNow;

        [Postgrest.Attributes.Column("Dark")] public bool Dark { get; set; }

        [Postgrest.Attributes.Column("ConvPayload")] public string ConvPayload { get; set; }
        [Postgrest.Attributes.Column("MessagePayload")] public string MessagePayload { get; set; }
        [Postgrest.Attributes.Column("UserPayload")] public string UserPayload { get; set; }


        [NotMapped]
        [JsonProperty("Notification")]
        public Dictionary<string, string>? Notification { get; set; }

        [Postgrest.Attributes.Column("Notification")]
        [Newtonsoft.Json.JsonIgnore]
        public string? NotificationJson
        {
            get => Notification == null ? null : System.Text.Json.JsonSerializer.Serialize(Notification);
            set => Notification = value == null ? null : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(value);
        }
    }
}
