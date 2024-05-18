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
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

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
        private readonly IHubContext<MessagesHub> _hubContext;

        
        public MessageController(IConfiguration configuration, Client supabaseClient, IHubContext<MessagesHub> hubContext)
        {
            _configuration = configuration;
            _supabaseClient = supabaseClient;
            _hubContext = hubContext;
        }
        [HttpPost("WebSocket")]
        public async Task<IActionResult> HandleWebhookEvent([FromBody] object payloadObject)
        {
            if (payloadObject == null)
            {
                Console.WriteLine("the paramerter returned is Null");

                return BadRequest("Payload is null");
            }
            // Cast the payload to a JObject
            string x =payloadObject.ToString();
            
            await _hubContext.Clients.All.SendAsync("ReceiveMessage", x);

            Console.WriteLine("Finished");
            var count = MessagesHub.GetConnectedClients();
            Console.WriteLine(count);

            /*
             type InsertPayload = {
  type: 'INSERT'
  table: string
  schema: string
  record: TableRecord<T>
  old_record: null
}
type UpdatePayload = {
  type: 'UPDATE'
  table: string
  schema: string
  record: TableRecord<T>
  old_record: TableRecord<T>
}
type DeletePayload = {
  type: 'DELETE'
  table: string
  schema: string
  record: null
  old_record: TableRecord<T>
}
            */


            return Ok();
        }

        [HttpPost("SendMessage"), Authorize]
        public async Task<IActionResult> SendMessage([FromBody] MessageUser messageUser)
        {
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return StatusCode(15, "Invalid Token");
            }
            var email = emailClaim.Value.Split(':')[0].Trim();
            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == email && n.Deleted == false)
                    .Get();
                var Sender = response.Models.FirstOrDefault();

                if (Sender == null)
                {
                    return StatusCode(10, "Invalid Token");
                }
                Console.WriteLine("Logged In");


                var messageQuery = await _supabaseClient.From<MessageDto>()
                    .Where(n => ((n.SenderId == Sender.Id && n.RecpientId == messageUser.RecpientId))) 
                    .Where(n=> (n.Deleted == false))
                    .Get();

                Console.WriteLine(messageQuery.Models.Count);
                var anotherMessageQuery = await _supabaseClient.From<MessageDto>()
                    .Where(n => ((n.SenderId == messageUser.RecpientId && n.RecpientId == Sender.Id)))
                    .Where(n => (n.Deleted == false))
                    .Get();


                Console.WriteLine(anotherMessageQuery.Models.Count);

                Console.WriteLine("Decision About to made");
                
                // Two Decisions Made here....Is is their first ever chat(Create a ConvTable) or not(update ConvTable)
                if (messageQuery.Models.Count != 0 || anotherMessageQuery.Models.Count!=0)
                    {
                    Console.WriteLine("Decision: Not a new message");

                    var fonkaParticipants = await _supabaseClient.From<ParticipantDto>()
                            .Where(p => p.UserId == Sender.Id)
                            .Get();
                    Console.WriteLine("Fonka Participants");

                    var barokParticipants = await _supabaseClient.From<ParticipantDto>()
                            .Where(p => p.UserId == messageUser.RecpientId)
                            .Get();
                    Console.WriteLine("Barok Participants");

                    Dictionary<long, long> myFirstDictionary = new Dictionary<long, long>();
                    Dictionary<long, long> mySeconDictionary = new Dictionary<long, long>();
                    
                    
                    myFirstDictionary=fonkaParticipants.Models.ToDictionary(f => f.ParticipantId, f2 => f2.ConversationId);
                    mySeconDictionary=barokParticipants.Models.ToDictionary(f => f.ParticipantId, f2 => f2.ConversationId);
                    
                    long ConvId = myFirstDictionary.Values.FirstOrDefault(values => mySeconDictionary.ContainsValue(values));
                    Console.WriteLine(ConvId);


                    if (ConvId == 0)
                        {
                            return BadRequest("Internal Server Error, ConvId not found when supposed to be found");
                        }

                        // I have the coversation Id
                        // Form the message to be sent to the database
                        MessageDto newMessageIf = new MessageDto
                        {
                            TimeStamp = DateTime.UtcNow,
                            MessageType = messageUser.MessageType,
                            Status = "Sent",
                            SenderId = Sender.Id,
                            RecpientId = messageUser.RecpientId,
                            Content = messageUser.Content,
                            ConvId = ConvId
                        };
                        // Update the Conversation Table and insert into the message table
                        
                            var insertResposne = await _supabaseClient.From<MessageDto>().Insert(newMessageIf);
                            
                            var messageResposneIf = insertResposne.Models.FirstOrDefault();
                    
                    // Update Conversation Table

                    var updateConvTable = await _supabaseClient.From<ConversationDto>()
                                .Where(n => n.ConvId == ConvId)
                                .Single();
                    updateConvTable.LastMessage = messageResposneIf.Id;
                    updateConvTable.UpdatedTime = messageResposneIf.TimeStamp;
                    
                    await updateConvTable.Update<ConversationDto>();


                    return Ok(messageResposneIf); 
                         

                    }
                Console.WriteLine("Decision: New Messsage");

                // Create new Conversation by meddling with Message,Conversation and Participation Table

                MessageDto newMessage = new MessageDto
                        {
                            TimeStamp = DateTime.UtcNow,
                            MessageType = messageUser.MessageType,
                            Status = "Sent",
                            SenderId = Sender.Id,
                            RecpientId = messageUser.RecpientId,
                            Content = messageUser.Content
                        };
                // Create a new Message Table with a convId of NULL temporarily
                Console.WriteLine("New message Formed");

                var insertMessage = await _supabaseClient.From<MessageDto>().Insert(newMessage);
                var messageResposne = insertMessage.Models.FirstOrDefault(); 

                Console.WriteLine("Message Inserted");
                // create a new row in the conversation table
                

                ConversationDto newConversation = new ConversationDto
                {
                    CreationTime = DateTime.UtcNow,
                    UpdatedTime = DateTime.UtcNow,
                    LastMessage = messageResposne.Id
                };

                var newConvTable = await _supabaseClient.From<ConversationDto>().Insert(newConversation);

                var newConvResponse = newConvTable.Models.FirstOrDefault();

                Console.WriteLine("Conversation Inserted");
                Console.WriteLine(newConvResponse.ConvId);

                // update the NULL convId back
                Console.WriteLine("Updating ConvId back....");


                var responseUpdateMessageId = await _supabaseClient.From<MessageDto>()
                                                .Where(n => n.Id == messageResposne.Id)
                                                .Single();
                
                responseUpdateMessageId.ConvId = newConvResponse.ConvId;
                await responseUpdateMessageId.Update<MessageDto>();
                                                
                Console.WriteLine("Updated ConvId back");

                ParticipantDto newParticipant1 = new ParticipantDto
                                            {
                                                UserId = Sender.Id,
                                                ConversationId = newConvResponse.ConvId
                                            };
                                            ParticipantDto newParticipant2 = new ParticipantDto
                                            {
                                                UserId = messageUser.RecpientId,
                                                ConversationId = newConvResponse.ConvId
                                            };

                                            var addNewParticipants1 = await _supabaseClient.From<ParticipantDto>()
                                                .Insert(newParticipant1);
                                            var addNewParticipants2 = await _supabaseClient.From<ParticipantDto>()
                                                .Insert(newParticipant2);
                Console.WriteLine("Created Participants");

                return Ok(messageResposne);
                                       
                                
            }
            catch (Exception)
            {
                return BadRequest("Connection Problem, Backend");
            }
        }
            
        [HttpPut("EditMessage"), Authorize]
        public async Task<IActionResult> EditMessage([FromBody] EditMessage editMessage)
        {
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return StatusCode(15, "Invalid Token");
            }
            var email = emailClaim.Value.Split(':')[0].Trim();
            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == email && n.Deleted == false)
                    .Get();
                var Sender = response.Models.FirstOrDefault();

                if (Sender == null)
                {
                    return StatusCode(10, "Invalid Token");
                }
                
                var updateMessage = await _supabaseClient.From<MessageDto>()
                    .Where(n => n.Id == editMessage.MessageId && n.SenderId == Sender.Id)
                    .Where(n => (n.Deleted == false))
                    .Single();
                updateMessage.Content = editMessage.Content;
                var hey = await updateMessage.Update<MessageDto>();
                var x = hey.Models.FirstOrDefault();
                
                return Ok(x);
            }
            catch (Exception)
            {
                return BadRequest("Connection Problem, Check if MessageId is valid");
            }

        }

        [HttpDelete("DeleteMessage"), Authorize]
        public async Task<IActionResult> DeleteMessage([FromBody] DeleteMessage deleteMessage)
        {
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return StatusCode(15, "Invalid Token");
            }
            var email = emailClaim.Value.Split(':')[0].Trim();
            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == email && n.Deleted == false)
                    .Get();
                var Sender = response.Models.FirstOrDefault();

                if (Sender == null)
                {
                    return StatusCode(10, "Invalid Token");
                }

                // Two Decisions here, Is it the last message in the conversation (Yes:delete Conv,participant,message)
                // (No:update the last message from conversation table and delete the message from the message table)
                var getMessageInfo = await _supabaseClient.From<MessageDto>()
                        .Where(n => n.Id == deleteMessage.MessageId && n.Deleted == false)
                        .Get();
                Console.WriteLine("Got the message");
                    var getRecpientId = getMessageInfo.Models.FirstOrDefault();
                    if (getRecpientId == null)
                    {
                        return BadRequest("Problem with the parameter Id");
                    }
                var messageQuery = await _supabaseClient.From<MessageDto>()
                    .Where(n => ((n.SenderId == Sender.Id && n.RecpientId == getRecpientId.RecpientId)))
                    .Where(n => (n.Deleted == false))
                    .Get();


                var anotherMessageQuery = await _supabaseClient.From<MessageDto>()
                    .Where(n => ((n.SenderId == getRecpientId.RecpientId && n.RecpientId == Sender.Id)))
                    .Where(n => (n.Deleted == false))
                    .Get();



                long checkCount = messageQuery.Models.Count + anotherMessageQuery.Models.Count;

                Console.WriteLine("Decision About to made");

                if (checkCount == 0)
                {
                    return BadRequest("Danger when fetching message");
                }
                
                    // execute first decision 
                    if (checkCount == 1)
                    {
                    
                            await _supabaseClient.From<ParticipantDto>()
                                .Where(n => n.ConversationId == getRecpientId.ConvId)
                                .Delete();
                            await _supabaseClient.From<ConversationDto>()
                                .Where(n => n.ConvId == getRecpientId.ConvId)
                                .Delete();
                    
                    var up = await _supabaseClient.From<MessageDto>()
                                .Where(n => n.Id == deleteMessage.MessageId)
                                .Single();
                            up.Deleted = true;
                            await up.Update<MessageDto>();
                    

                    return Ok("Deleted");
                        

                    }

                // Second Decision tree
                Console.WriteLine("Second Decsion Tree");
                        var getConvid = await _supabaseClient.From<ConversationDto>()
                            .Where(n => n.ConvId == getRecpientId.ConvId)
                            .Get();
                        // Is it the Last message or not?
                        //Yes it is
                        var getconvId = getConvid.Models.FirstOrDefault();
                Console.WriteLine("Got Conversation Id");


                if (deleteMessage.MessageId == getconvId.LastMessage)
                        {
                    Console.WriteLine("Correct Message Id");

                    // First part: where Sender is Sender.Id and Recipient is getRecpientId.RecpientId
                    var messagesSent = await _supabaseClient.From<MessageDto>()
                        .Where(n => n.SenderId == Sender.Id && n.RecpientId == getRecpientId.RecpientId)
                        .Where(n => n.Deleted == false)
                        .Get();

                    // Second part: where Sender is getRecpientId.RecpientId and Recipient is Sender.Id
                    var messagesReceived = await _supabaseClient.From<MessageDto>()
                        .Where(n => n.SenderId == getRecpientId.RecpientId && n.RecpientId == Sender.Id)
                        .Where(n=> n.Deleted == false)
                        .Get();
                  
                    // Combine the results and order by TimeStamp descending
                    var combinedMessages = messagesSent.Models.Concat(messagesReceived.Models)
                        .OrderByDescending(m => m.TimeStamp)
                        .ToList();

                    // Get the latest message


                    var SecondconvId = combinedMessages[1];
                    var hey2 = await _supabaseClient.From<ConversationDto>()
                                .Where(n => n.ConvId == getRecpientId.ConvId)
                                .Single();
                    
                         hey2.LastMessage = SecondconvId.Id;
                         await hey2.Update<ConversationDto>();
                    Console.WriteLine("Done Updating");

                    
                }

                var antoherHey = await _supabaseClient.From<MessageDto>()
                    .Where(n => n.Id == deleteMessage.MessageId)
                    .Single();
                antoherHey.Deleted = true;

                await antoherHey.Update<MessageDto>();
                            


                return Ok("Deleted");

            }
            catch(Exception)
            {
                return BadRequest ("Problem when deleting a message");
            }
        }

        [HttpGet("GetMessageHistory"), Authorize]
        public async Task<IActionResult> GetMessageHistory(string query)
            {
                
                try
                {
                    var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
                    if (emailClaim == null)
                    {
                        return BadRequest("Invalid Token");
                    }


                    var getSender = await _supabaseClient.From<Userdto>()
                        .Where(n => n.Email == emailClaim.ToString() && n.Deleted == false)
                        .Get();
                    var sender = getSender.Models.FirstOrDefault();

                    if (sender == null)
                    {
                        return BadRequest("Invalid Token");
                    }

                    var getEverything = await _supabaseClient.From<MessageDto>()
                        .Where(n => (n.SenderId == sender.Id) || (n.RecpientId == sender.Id) && n.Deleted==false)
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

                var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
                if (emailClaim == null)
                {
                    return BadRequest("Invalid Token");
                }

                try
                {

                    var GetSender = await _supabaseClient.From<Userdto>()
                        .Where(n => n.Email == emailClaim.ToString()&& n.Deleted==false).Get();


                    try
                    {
                        var Sender = GetSender.Models.FirstOrDefault();


                        if (Sender == null)
                        {
                            return BadRequest("Invalid Token");
                        }

                        var fonkaParticipants = await _supabaseClient
                            .From<ParticipantDto>()
                            .Where(p => p.UserId == Sender.Id )
                            .Get();

                        var barokParticipants = await _supabaseClient
                            .From<ParticipantDto>()
                            .Where(p => p.UserId == messageUser.RecpientId)
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
                            .Where(n => n.ConvId == ConvId)
                            .Where(n => n.Content == messageUser.Content)
                            .Where(n=>n.Deleted==false)
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
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {

                var GetSender = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == emailClaim.ToString() && n.Deleted == false).Get();



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
                            .Where(p => p.UserId == Sender.Id)
                            .Get();

                        var barokParticipants = await _supabaseClient
                            .From<ParticipantDto>()
                            .Where(p => p.UserId == talkee)
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
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return BadRequest("Invalid Token");
            }

            try
            {

                var GetSender = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == emailClaim.ToString() && n.Deleted == false).Get();



                try
                {
                    var Sender = GetSender.Models.FirstOrDefault();


                    if (Sender == null)
                    {
                        return BadRequest("Invalid Token");
                    }

                    // get Conversation info
                    var convInfo = await _supabaseClient.From<ConversationDto>()
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

        public async Task<IActionResult> GetConversationMessage([FromBody] GetConvMessageModel getConvMessageModel)
        {
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return StatusCode(15, "Invalid Token");
            }
            var email = emailClaim.Value.Split(':')[0].Trim();
            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == email && n.Deleted == false)
                    .Get();
                var hey = response.Models.FirstOrDefault();

                if (hey == null)
                {
                    return StatusCode(10, "Invalid Token");
                }

                // get Conversation info 
                var convMessage = await _supabaseClient.From<MessageDto>()
                        .Where(n => n.ConvId == getConvMessageModel.ConvId && n.Deleted==false)
                        .Where(n=> n.SenderId == hey.Id || n.RecpientId == hey.Id)
                        .Order(n => n.TimeStamp, Constants.Ordering.Ascending)
                        .Get();
                    var allmessArray = convMessage.Models.ToList();
                    return Ok(allmessArray);
                
            }
            catch (Exception)
            {
                return BadRequest("Connection Problem first part");
            }
        }

        [HttpGet("GetAllConversationDirect"), Authorize]
        
        public async Task<IActionResult> GetAllConversationDirect()
        {
            
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return StatusCode(15, "Invalid Token");
            }
            var email = emailClaim.Value.Split(':')[0].Trim();
            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == email && n.Deleted == false)
                    .Get();
                var hey = response.Models.FirstOrDefault();
                
                if (hey == null)
                {
                    return StatusCode(10, "Invalid Token");
                }

               var allConvIdResponse = await _supabaseClient.From<ParticipantDto>()
                     .Where(n => n.UserId == hey.Id)
                     .Get();

               var allConvIdArray = allConvIdResponse.Models.Select(n => n.ConversationId).ToHashSet();
                List<ConversationDto> allConvIdOrdered = new List<ConversationDto>();
                List<CustomConv> allyouNeed = new List<CustomConv>();
                
                // Get all Conversation info ordered by UpdatedTime
                    foreach (var convId in allConvIdArray)
                    {

                        var convResponse = await _supabaseClient.From<ConversationDto>()
                            .Where(n => n.ConvId == convId)
                            .Get();
                        var messageIdConv = convResponse.Models.FirstOrDefault();
                        long messageId = messageIdConv.LastMessage;
                        
                    var messResponse = await _supabaseClient.From<MessageDto>()
                        .Where(n => n.Id == messageId)
                        .Get();
                    var getContent = messResponse.Models.FirstOrDefault();
                    string content = getContent.Content;

                    var userResponse = await _supabaseClient.From<ParticipantDto>()
                            .Where(n => n.ConversationId == convId)
                            .Get();
                    var userRes = userResponse.Models.ToList();
                    var first = userRes[0];
                    var second = userRes[1];
                    long UserId = 0;
                    if (first.UserId == second.UserId)
                    {
                        UserId = second.UserId;
                        Console.WriteLine("Saved Messages");

                    }
                    else if (first.UserId != hey.Id)
                    {
                        UserId = second.UserId;
                    }
                    else if (second.UserId!=hey.Id) {
                        UserId = first.UserId;
                    }
                    else
                    {
                        return BadRequest(" Problem when getting UserId from Conversation Id");
                    }
                    if (UserId == 0)
                    {
                        return BadRequest("UserId is 0");
                    }
                    var getProfile = await _supabaseClient.From<UserProfiledto>()
                        .Where(n => n.UserId == UserId)
                        .Get();
                    var getProfile2 = getProfile.Models.FirstOrDefault();
                    var userName = getProfile2.Name;

                    CustomConv xz = new CustomConv
                    {
                        UserName = userName,
                        UpdatedTime = messageIdConv.UpdatedTime,
                        Message = content,
                        ConversationId = convId
                    };
                        
                        allyouNeed.Add(xz);
                    }
                    var orderedResults = allyouNeed.OrderByDescending(n => n.UpdatedTime).ToList();

                 return Ok(orderedResults);
                                
            }
            catch (Exception)
            {
                return StatusCode(30,"Connection Problem, Backend"); 
            }
        }

        [HttpDelete("DeleteConversation"), Authorize]
        
        public async Task<IActionResult> DeleteConversaion([FromBody] DeleteConv deleteConv)
        {
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return StatusCode(15, "Invalid Token");
            }
            var email = emailClaim.Value.Split(':')[0].Trim();
            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == email && n.Deleted == false)
                    .Get();
                var hey = response.Models.FirstOrDefault();

                if (hey == null)
                {
                    return StatusCode(10, "Invalid Token");
                }
                // Delete all messages
                // Delete Participants
                // Delete Conversation finally
                Console.WriteLine(deleteConv.Id);
               
                    var up = await _supabaseClient.From<MessageDto>()
                     .Where(n => n.ConvId == deleteConv.Id)
                     .Single();
                    if (up == null)
                    {
                        return BadRequest("up is null");
                    }
                    up.Deleted = true;
                    await up.Update<MessageDto>();
                    
                
                /*
                 var up = await _supabaseClient.From<MessageDto>()
                                .Where(n => n.Id == deleteMessage.MessageId)
                                .Single();
                            up.Deleted = true;
                            await up.Update<MessageDto>();
                */
                await _supabaseClient.From<ParticipantDto>()
                        .Where(n => n.ConversationId == deleteConv.Id)
                        .Delete();
                Console.WriteLine("second");

                await _supabaseClient.From<ConversationDto>()
                    .Where(n => n.ConvId == deleteConv.Id)
                    .Delete();

                Console.WriteLine("thrid");

                
                





                return Ok("Successfully Deleted");
                
            }
            catch
            {
                return BadRequest("Connection Problem");
            }
        }
    }
}