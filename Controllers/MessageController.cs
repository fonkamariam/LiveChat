using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using LiveChat.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Supabase.Interfaces;
using Supabase;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;

namespace LiveChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessageController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly Client _supabaseClient;

        public MessageController(IConfiguration configuration, Client supabaseClient)
        {
            _configuration = configuration;
            _supabaseClient = supabaseClient;
        }

        [HttpPost("SendMessage"), Authorize]
        public async Task<IActionResult> SendMessage(MessageUser messageUser)
        {
            var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
            if (phoneNumberClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {

                var GetSender = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();


                try
                {
                    var Sender = GetSender.Models.FirstOrDefault();


                    if (Sender == null)
                    {
                        return BadRequest("Invalid Token");
                    }


                    var messageQuery = await _supabaseClient
                        .From<MessageDto>()
                        .Where(n => ((((n.SenderId == Sender.Id)
                                       && (n.RecpientId == messageUser.RecpientId))
                                      && (n.ChatType == messageUser.ChatType))
                                     || ((n.SenderId == messageUser.RecpientId) &&
                                         (n.RecpientId == Sender.Id)) &&
                                     (n.ChatType == messageUser.ChatType)))
                        .Get();
                    // Two Decisions Made here....Is is their first ever chat(Create a ConvTable) or not(update ConvTable)
                    if (messageQuery.Models.Count != 0)
                    {

                        var fonkaParticipants = await _supabaseClient
                            .From<ParticipantDto>()
                            .Where(p => p.UserId == Sender.Id && p.ChatType == messageUser.ChatType)
                            .Get();

                        var barokParticipants = await _supabaseClient
                            .From<ParticipantDto>()
                            .Where(p => p.UserId == messageUser.RecpientId && p.ChatType == messageUser.ChatType)
                            .Get();

                        Dictionary<long, long> myFirstDictionary = new Dictionary<long, long>();
                        Dictionary<long, long> mySeconDictionary = new Dictionary<long, long>();

                        myFirstDictionary =
                            fonkaParticipants.Models.ToDictionary(f => f.ConversationId, f2 => f2.ParticipantId);
                        mySeconDictionary =
                            barokParticipants.Models.ToDictionary(f => f.ConversationId, f2 => f2.ParticipantId);

                        long ConvId = 0;
                        foreach (var key in myFirstDictionary.Keys)
                        {

                            if (mySeconDictionary.ContainsKey(myFirstDictionary[key]))
                            {
                                ConvId = myFirstDictionary[key];
                                break;
                            }
                        }

                        if (ConvId == 0)
                        {
                            return BadRequest("Internal Server Error, ConvId not found when supposed to be found");
                        }

                        // I have the coversation Id
                        // Form the message to be sent to the database
                        MessageDto newMessage = new MessageDto
                        {
                            TimeStamp = DateTime.UtcNow,
                            ChatType = messageUser.ChatType,
                            MessageType = messageUser.MessageType,
                            Status = "Sent",
                            SenderId = Sender.Id,
                            RecpientId = messageUser.RecpientId,
                            Content = messageUser.Content,
                            ConvId = ConvId
                        };
                        // Update the Conversation Table and insert into the message table
                        try
                        {
                            var insertResposne = await _supabaseClient.From<MessageDto>().Insert(newMessage);
                            try
                            {
                                var messageResposne = insertResposne.Models.FirstOrDefault();
                                // Update Conversation Table
                                try
                                {
                                    var updateConvTable = await _supabaseClient.From<ConversatinDto>()
                                        .Where(n => n.ConvId == ConvId)
                                        .Set(n => n.LastMessage, messageResposne.Id)
                                        .Set(n => n.UpdatedTime, messageResposne.TimeStamp)
                                        .Update();

                                    return Ok(newMessage);
                                }
                                catch (Exception)
                                {
                                    return BadRequest("Can't connect to server");
                                }
                            }
                            catch (Exception)
                            {
                                return BadRequest("Message Not inserted Properly");
                            }
                        }
                        catch (Exception)
                        {
                            return BadRequest("Can't connect to server");
                        }


                    }

                    // Create new Conversation by meddling with Message,Conversation and Participation Table
                    try
                    {
                        MessageDto newMessage = new MessageDto
                        {
                            TimeStamp = DateTime.UtcNow,
                            ChatType = messageUser.ChatType,
                            MessageType = messageUser.MessageType,
                            Status = "Sent",
                            SenderId = Sender.Id,
                            RecpientId = messageUser.RecpientId,
                            Content = messageUser.Content,
                            ConvId = 0
                        };
                        // Create a new Message Table with a convId of 0 temporarily
                        var insertMessage = await _supabaseClient.From<MessageDto>().Insert(newMessage);
                        try
                        {
                            var messageResposne = insertMessage.Models.FirstOrDefault();
                            if (messageResposne == null)
                            {
                                return BadRequest("Problem at getting a message Id");
                            }

                            // create a new row in the conversation table
                            try
                            {
                                ConversatinDto newConversation = new ConversatinDto
                                {
                                    CreationTime = DateTime.UtcNow,
                                    UpdatedTime = DateTime.Now,
                                    LastMessage = messageResposne.Id,
                                    Type = messageResposne.ChatType

                                };
                                try
                                {
                                    var newConvTable =
                                        await _supabaseClient.From<ConversatinDto>().Insert(newConversation);
                                    try
                                    {
                                        var newConvResponse = newConvTable.Models.FirstOrDefault();
                                        if (newConvResponse == null)
                                        {
                                            return BadRequest("Problem at getting a message Id");
                                        }

                                        // update the 0 convId back
                                        try
                                        {

                                            var responseUpdateMessageId = await _supabaseClient.From<MessageDto>()
                                                .Where(n => n.Id == messageResposne.Id)
                                                .Set(u => u.ConvId, newConvResponse.ConvId)
                                                .Update();
                                            ParticipantDto newParticipant1 = new ParticipantDto
                                            {
                                                UserId = Sender.Id,
                                                ChatType = messageUser.ChatType,
                                                ConversationId = newConvResponse.ConvId
                                            };
                                            ParticipantDto newParticipant2 = new ParticipantDto
                                            {
                                                UserId = messageUser.RecpientId,
                                                ChatType = messageUser.ChatType,
                                                ConversationId = newConvResponse.ConvId
                                            };

                                            var addNewParticipants1 = await _supabaseClient.From<ParticipantDto>()
                                                .Insert(newParticipant1);
                                            var addNewParticipants2 = await _supabaseClient.From<ParticipantDto>()
                                                .Insert(newParticipant2);
                                            return Ok(newMessage);
                                        }
                                        catch (Exception)
                                        {
                                            return BadRequest("No Connection creating new participants");
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        return BadRequest("Problem inserting a new Conversation");
                                    }
                                }
                                catch (Exception)
                                {
                                    return BadRequest(
                                        "Connection Problem when inserting a new conversation to the conversation table");
                                }

                            }
                            catch (Exception)
                            {
                                return BadRequest("Can't connect to server");
                            }
                        }
                        catch (Exception)
                        {
                            return BadRequest("Connection Problem, please try again");
                        }

                    }
                    catch (Exception)
                    {
                        return BadRequest("Problem when querying the database");
                    }
                }
                catch (Exception)
                {
                    return BadRequest("No Connection, Please Try again");
                }
            }
            catch (Exception)
            {
                return BadRequest("UserProblem");
            }
        }


        [HttpPut("EditMessage"), Authorize]
        public async Task<IActionResult> EditMessage(long parameterId,string parameterContent)
        {
            try
            {
                var updateMessage = await _supabaseClient.From<MessageDto>().Where(n => n.Id == parameterId)
                    .Set(n => n.Content, parameterContent)
                    .Update();
                var hey = updateMessage.Models.FirstOrDefault();
                if (hey == null)
                {
                    return BadRequest("Problem with the parameter Id");
                }
                return Ok(hey);
            }
            catch (Exception)
            {
                return BadRequest("Connection Problem");
            }
            
        }

        
    }
}
