using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using LiveChat.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Supabase.Interfaces;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using Postgrest;
using System.Linq; // For Select method

using Client = Supabase.Client;
using System.Linq;

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
        public async Task<IActionResult> EditMessage(long parameterId, string parameterContent)
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

        [HttpDelete("DeleteMessage"), Authorize]
        public async Task<IActionResult> DeleteMessage(long parameterId)
        {
            try
            {
                var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
                if (phoneNumberClaim == null)
                {
                    return BadRequest("Invalid Token");
                }

                var getSender = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();

                var sender = getSender.Models.FirstOrDefault();

                if (sender == null)
                {
                    return BadRequest("Invalid Token");
                }

                // Two Decisions here, Is it the last message in the conversation (Yes:delete Conv,participant,message)
                // (No:update the last message from conversation table and delete the message from the message table)
                var getMessageInfo = await _supabaseClient.From<MessageDto>()
                    .Where(n => n.Id == parameterId)
                    .Get();

                var getRecpientId = getMessageInfo.Models.FirstOrDefault();
                if (getRecpientId == null)
                {
                    return BadRequest("Problem with the parameter Id");
                }

                var checkMessageTable = await _supabaseClient.From<MessageDto>()
                    .Where(n => ((n.SenderId == sender.Id || n.SenderId == getRecpientId.RecpientId) &&
                                 (n.RecpientId == sender.Id || n.RecpientId == getRecpientId.RecpientId) &&
                                 n.ChatType == getRecpientId.ChatType))
                    .Get();
                var count = checkMessageTable.Models.Count();
                if (count == 0)
                {
                    return BadRequest("Danger when fetching message");
                }

                // execute first decision 
                if (count == 1)
                {
                    try
                    {
                        await _supabaseClient.From<ParticipantDto>()
                            .Where(n => n.ConversationId == getRecpientId.ConvId)
                            .Delete();
                        await _supabaseClient.From<ConversatinDto>()
                            .Where(n => n.ConvId == getRecpientId.ConvId)
                            .Delete();
                        await _supabaseClient.From<MessageDto>()
                            .Where(n => n.Id == parameterId)
                            .Delete();
                        return Ok("Deleted");
                    }
                    catch (Exception)
                    {
                        return BadRequest(
                            "Problem deleting one of Participants, Conversation or Message from their respective table");
                    }


                }

                // Second Decision tree
                try
                {
                    var getConvid = await _supabaseClient.From<ConversatinDto>()
                        .Where(n => n.ConvId == getRecpientId.ConvId)
                        .Get();
                    // Is it the Last message or not?
                    //Yes it is
                    var getconvId = getConvid.Models.FirstOrDefault();


                    if (parameterId == getconvId.LastMessage)
                    {
                        var lastestLastMessage = await _supabaseClient.From<MessageDto>()
                            .Where(n => ((n.SenderId == sender.Id || n.SenderId == getRecpientId.RecpientId) &&
                                         (n.RecpientId == sender.Id || n.RecpientId == getRecpientId.RecpientId) &&
                                         n.ChatType == getRecpientId.ChatType))
                            .Order(n => n.TimeStamp, Constants.Ordering.Descending)
                            .Get();

                        var secondlastMessage = lastestLastMessage.Models.Skip(1).FirstOrDefault();
                        await _supabaseClient.From<ConversatinDto>()
                            .Where(n => n.ConvId == getRecpientId.ConvId)
                            .Set(n => n.LastMessage, secondlastMessage.Id)
                            .Update();
                    }

                    await _supabaseClient.From<MessageDto>()
                        .Where(n => n.Id == parameterId)
                        .Delete();


                    return Ok("Deleted");
                }
                catch (Exception)
                {
                    return BadRequest(
                        "Problem either deleting Message or updating fetching latest message and updating it from the message table");
                }
            }
            catch (Exception)
            {
                return BadRequest("No Connection");
            }
        }

        [HttpGet("GetMessageHistory"), Authorize]
        public async Task<IActionResult> GetMessageHistory(string query)
        {
            try
            {
                var phoneNumberClaim = User.Claims.FirstOrDefault(c => c.Type == "PhoneNumber");
                if (phoneNumberClaim == null)
                {
                    return BadRequest("Invalid Token");
                }

                var getSender = await _supabaseClient.From<Userdto>()
                    .Where(n => n.PhoneNo == phoneNumberClaim.ToString()).Get();

                var sender = getSender.Models.FirstOrDefault();

                if (sender == null)
                {
                    return BadRequest("Invalid Token");
                }

                var getEverything = await _supabaseClient.From<MessageDto>()
                    .Where(n => (n.SenderId == sender.Id) || (n.RecpientId == sender.Id))
                    .Where(n => n.Content.Contains(query))
                    .Order(n => n.TimeStamp, Constants.Ordering.Descending)
                    .Get();

                Array heygetEverything = getEverything.Models.ToArray();
                return Ok(heygetEverything);


            }
            catch (Exception)
            {
                return BadRequest("Connection Problem");
            }
        }

        [HttpGet("GetMessageId"), Authorize]
        public async Task<IActionResult> GetMessageId(MessageUser messageUser)
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
                    // Get the message Id from the Database
                    var getMessageinfo = await _supabaseClient.From<MessageDto>()
                        .Where(n => n.SenderId == Sender.Id)
                        .Where(n => n.RecpientId == messageUser.RecpientId)
                        .Where(n => n.ChatType == messageUser.ChatType)
                        .Where(n => n.ConvId == ConvId)
                        .Where(n => n.Content == messageUser.Content)
                        .Get();
                    var messageId = getMessageinfo.Models.FirstOrDefault();
                    if (messageId == null)
                    {
                        return BadRequest("Internal Server Error when fetching message Info, Consult Builder");
                    }

                    return Ok(messageId.Id);


                }
                catch (Exception)
                {
                    return BadRequest("Connection Problem");
                }

            }
            catch
            {
                return BadRequest("Connection Problem");
            }
        }

        [HttpGet("GetConversationId"), Authorize]

        public async Task<IActionResult> GetConversationId(long talkee, string parameterType)
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

                    // get Conversation Id
                    var fonkaParticipants = await _supabaseClient
                        .From<ParticipantDto>()
                        .Where(p => p.UserId == Sender.Id && p.ChatType == parameterType)
                        .Get();

                    var barokParticipants = await _supabaseClient
                        .From<ParticipantDto>()
                        .Where(p => p.UserId == talkee && p.ChatType == parameterType)
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
                        return BadRequest("No Conversation with user with provided user");
                    }

                    // I have the coversation Id
                    return Ok(ConvId);

                }
                catch (Exception)
                {
                    return BadRequest("Connection Problem in second part");
                }
            }
            catch (Exception)
            {
                return BadRequest("Connection Problem first part");
            }
        }


        [HttpGet("GetConversationInfo"), Authorize]

        public async Task<IActionResult> GetConversationInfo(long parameterConvId)
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

                    // get Conversation info
                    var convInfo = await _supabaseClient.From<ConversatinDto>()
                        .Where(n => n.ConvId == parameterConvId)
                        .Get();
                    var conversationInfo = convInfo.Models.FirstOrDefault();
                    return Ok(conversationInfo);
                }
                catch (Exception)
                {
                    return BadRequest("Problem in the conversation Id in the parameter");
                }
            }
            catch (Exception)
            {
                return BadRequest("Connection Problem first part");
            }
        }

        [HttpGet("GetConversationMessage"), Authorize]

        public async Task<IActionResult> GetConversationMessage(long parameterConvId)
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
                    // get Conversation info 
                    var convMessage = await _supabaseClient.From<MessageDto>()
                        .Where(n => n.ConvId == parameterConvId)
                        .Order(n=>n.TimeStamp,Constants.Ordering.Ascending)
                        .Get();
                    Array allmessArray = convMessage.Models.ToArray();
                    return Ok(allmessArray);
                } 
                catch (Exception) 
                {
                    return BadRequest("Problem in the conversation Id in the parameter");
                }
            }
            catch (Exception)
            {
                return BadRequest("Connection Problem first part");
            }
        }

        [HttpGet("GetAllConversationDirect"), Authorize]

        public async Task<IActionResult> GetAllConversationDirect()
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
                    // get all Conversation info
                    var allConvId = await _supabaseClient.From<ParticipantDto>()
                        .Where(n => n.UserId == Sender.Id && n.ChatType=="Direct")
                        .Select("ConversationId")
                        .Get();
                    var allConvIdArray = allConvId.Models.ToArray();
                    
                    var allConvIdOrdered = await _supabaseClient.From<ConversatinDto>()
                        .Where(n => allConvIdArray.Contains<>(n.ConvId))
                        .Order(n=> n.UpdatedTime,Constants.Ordering.Descending)
                        .Get();

                    var final= allConvIdOrdered.Models.ToArray();

                    // Check if there is a conversation between self
                    var fonkaParticipants = await _supabaseClient.From<ParticipantDto>()
                        .Where(n => n.UserId == Sender.Id && n.ChatType == "Direct")
                        .Get();
                    var convIdsWithTwoFonkaParticipants = fonkaParticipants.Models
                        .GroupBy(n => n.ConversationId)
                        .Where(g => g.Count() == 2)
                        .SelectMany(g => g.Select(p => p.ConversationId))
                        .Distinct()
                        .ToList();
                    var checkOwn = await _supabaseClient.From<ParticipantDto>()
                        .Where(n => convIdsWithTwoFonkaParticipants.Contains(n.ConversationId))
                        .Get();
                   
                    if (checkOwn.Models.Count == 2)
                    {
                        var ConvIdOwn = checkOwn.Models.FirstOrDefault();
                        var GetownConv = await _supabaseClient.From<ConversatinDto>()
                            .Where(n => n.ConvId == ConvIdOwn.ConversationId)
                            .Get();
                        var tobeaAppended = GetownConv.Models.FirstOrDefault();

                        final.Append(tobeaAppended);
                    }

                    var dicUserConv = await _supabaseClient.From<ParticipantDto>()
                        .Where(n => (final.Contains<>(n.ConversationId))&&(n.UserId!=Sender.Id))
                        .Get();
                    
                    
                    var dicUserConvFinal = dicUserConv.Models.OrderBy(p => Array.IndexOf(final, p.ConversationId)).ToArray();
                    
                    return Ok(dicUserConvFinal);
                }
                catch (Exception)
                {
                    return BadRequest("Problem in the conversation Id in the parameter");
                }
            }
            catch (Exception)
            {
                return BadRequest("Connection Problem first part");
            }
        }


        [HttpGet("GetAllConversationGroup"), Authorize]

        public async Task<IActionResult> GetAllConversationGroup()
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
                    // get all Conversation info
                    var allConvId = await _supabaseClient.From<ParticipantDto>()
                        .Where(n => n.UserId == Sender.Id && n.ChatType == "Group")
                        .Select("ConversationId")
                        .Get();
                    var allConvIdArray = allConvId.Models.ToArray();

                    var allConvIdOrdered = await _supabaseClient.From<ConversatinDto>()
                        .Where(n => allConvIdArray.Contains<>(n.ConvId))
                        .Order(n => n.UpdatedTime, Constants.Ordering.Descending)
                        .Get();

                    var final = allConvIdOrdered.Models.ToArray();

                    

                    var dicUserConv = await _supabaseClient.From<ParticipantDto>()
                        .Where(n => (final.Contains<>(n.ConversationId)) && (n.UserId != Sender.Id))
                        .Get();


                    var dicUserConvFinal = dicUserConv.Models.OrderBy(p => Array.IndexOf(final, p.ConversationId)).ToArray();





                    return Ok(dicUserConvFinal);
                }
                catch (Exception)
                {
                    return BadRequest("Problem in the conversation Id in the parameter");
                }
            }
            catch (Exception)
            {
                return BadRequest("Connection Problem first part");
            }
        }

    }
}
