﻿using Microsoft.AspNetCore.Http;
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
    [ApiController]
    [Route("api/[controller]")]
    public class MessageController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly Client _supabaseClient;
        private readonly IHubContext<MessagesHub> _hubContext;
        private readonly UserConnectionManager _userConnectionManager;


        public MessageController(IConfiguration configuration, Client supabaseClient, IHubContext<MessagesHub> hubContext, UserConnectionManager userConnectionManager)
        {
            _configuration = configuration;
            _supabaseClient = supabaseClient;
            _hubContext = hubContext;
            _userConnectionManager = userConnectionManager;
        }

        [HttpGet("GetMissedPayloads"),Authorize]
        public async Task<IActionResult> GetMissedPayloads()
        {
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return Unauthorized("Invalid Token");
            }
            var email = emailClaim.Value.Split(':')[0].Trim();
            try
            {
                var response = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Email == email && n.Deleted == false)
                    .Single();

                if (response == null)
                {
                    return Unauthorized("Invalid Token");
                }

                var allPayloads = new List<object>();
                Console.WriteLine("1");
                if (response.MessagePayload == null && response.UserPayload == null && response.ConvPayload == null && response.OnlinePayload == null)
                {
                    Console.WriteLine("1.5");

                    return Ok(allPayloads);
                }
                Console.WriteLine("2");

                if (response.MessagePayload != null)
                {
                    Console.WriteLine("2.5");

                    var allMessagePayloads = JsonConvert.DeserializeObject<List<PayLoad>>(response.MessagePayload);
                    allPayloads.AddRange(allMessagePayloads);
                    Console.WriteLine("2.6");

                }
                Console.WriteLine("3");

                if (response.UserPayload != null)
                {
                    Console.WriteLine("3.5");

                    var allUserPayloads = JsonConvert.DeserializeObject<List<UserPayLoad>>(response.UserPayload);
                    allPayloads.AddRange(allUserPayloads);
                    Console.WriteLine("3.6");

                }
                Console.WriteLine("4");

                if (response.ConvPayload != null)
                {
                    Console.WriteLine("4.5");

                    var allConvPayloads = JsonConvert.DeserializeObject<List<ConvPayLoad>>(response.ConvPayload);
                    allPayloads.AddRange(allConvPayloads);
                    Console.WriteLine("4.6");

                }
                Console.WriteLine("5");
                if (response.OnlinePayload != null)
                {
                    Console.WriteLine("5.5");

                    var allOnlinePayloads = JsonConvert.DeserializeObject<Dictionary<string, UserStatusDic>>(response.OnlinePayload);
                    var allOnlinePayloadsArray = allOnlinePayloads.Select(kvp => new { Key = kvp.Key, Value = kvp.Value }).ToArray();
                    allPayloads.AddRange(allOnlinePayloadsArray);
                    Console.WriteLine("5.6");
                }
                // Clear the payloads
                response.MessagePayload = null;
                response.ConvPayload = null;
                response.UserPayload = null;
                response.OnlinePayload = null;

                await response.Update<Userdto>();
                Console.WriteLine("6");

                Console.WriteLine("Given all 3 payloads and set them to null");

                return Ok(allPayloads);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return BadRequest("Connection Problem first part");
            }
        }
        
        [HttpPost("WebSocketMessage")]
        public async Task<IActionResult> HandleMessageEvent([FromBody] object payloadObject)
        {
            if (payloadObject == null)
            {
                Console.WriteLine("the paramerter returned is Null");

                return BadRequest("Payload is null");
            }
            // Cast the payload to a JObject
            string x =payloadObject.ToString();
            PayLoad payLoad = JsonConvert.DeserializeObject<PayLoad>(x); 
            
            if (payLoad.type == "INSERT")
            {
                
                var recp = payLoad.record.RecpientId;
                var sender = payLoad.record.SenderId;
                long convIdJson = payLoad.record.ConvId;

                var response = await _supabaseClient.From<UserProfiledto>()
                    .Where(n => n.UserId == sender && n.Deleted == false)
                    .Get();
                var responseModels = response.Models.FirstOrDefault();
                if (responseModels == null)
                {
                    return BadRequest("Problem getting user profile in ws for conversation");
                }
                // for fetching email
                var response12 = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Id == sender && n.Deleted == false)
                    .Get();
                var responseModels12 = response12.Models.FirstOrDefault();
                if (responseModels12 == null)
                {

                    return BadRequest("Problem getting user profile in ws for conversation");
                }
                payLoad.record.Status = responseModels.Name;
                payLoad.record.MessageType = responseModels.LastName;

                // sending status,lastSeen,bio,ProfilePicConv
                

                payLoad.record.OnlineStatus = responseModels12.Status;
                payLoad.record.Bio = responseModels.Bio;
                payLoad.record.LastSeen = responseModels12.LastSeen;
                payLoad.record.ProfilePic = responseModels.ProfilePic;
                payLoad.record.Email = responseModels12.Email;



         

                if (recp!= sender)
                {
                    Console.WriteLine("INSERT Message Payload");
                    _userConnectionManager.TryGetValue(recp.ToString(),out var userInfoo);
                    Console.WriteLine(userInfoo.IsActive);

                    if (_userConnectionManager.TryGetValue(recp.ToString(), out var userInfo) && userInfo.IsActive)
                    {
                        // Send message to active recipient
                        Console.WriteLine($"INSERT Message Payload: {recp.ToString()}   Active");

                        await _hubContext.Clients.Group(recp.ToString()).SendAsync("ReceiveMessage", payLoad);
                        Console.WriteLine("await _hubcontext");

                    }
                    else 
                    {
                        Console.WriteLine("INSERT Message Payload:    InActive");

                        var getArrayModel = await _supabaseClient.From<Userdto>()
                         .Where(n => n.Id == recp && n.Deleted == false)
                         .Single();
                        //var getArrayModel = getArray.Models.FirstOrDefault();
                        if (getArrayModel.MessagePayload == null) 
                        {                      
                            List<PayLoad> emptyProfilePic = [];
                            emptyProfilePic.Add(payLoad);
                            string jsonListEmpty = JsonConvert.SerializeObject(emptyProfilePic);
                            getArrayModel.MessagePayload = jsonListEmpty;
                            await getArrayModel.Update<Userdto>();
                            Console.WriteLine("ADded empty");
                            return Ok("Added to an empty array");
                        }


                        List<PayLoad> allProfilePic = JsonConvert.DeserializeObject<List<PayLoad>>(getArrayModel.MessagePayload);
                        
                        allProfilePic.Add(payLoad);

                        string jsonList = JsonConvert.SerializeObject(allProfilePic);


                        getArrayModel.MessagePayload = jsonList;

                        await getArrayModel.Update<Userdto>();
                        Console.WriteLine("aDDed Payload");

                        // Store payload for inactive recipient
                        Console.WriteLine($"Inserted to Stored for userId:{recp}");
                        //await StorePayloadForUserAsync(recp, payLoad);
                    }
                    //await _hubContext.Clients.Group(recp.ToString()).SendAsync("ReceiveMessage", payLoad);

                }

            }
            else if (payLoad.type == "UPDATE" && payLoad.record.Deleted == false) { 
                var recp = payLoad.record.RecpientId;
                var sender = payLoad.record.SenderId;

                if (recp != sender)
                {
                    if (_userConnectionManager.TryGetValue(recp.ToString(), out var userInfo) && userInfo.IsActive)
                    {
                        // Send message to active recipient
                        await _hubContext.Clients.Group(recp.ToString()).SendAsync("ReceiveMessage", payLoad);
                    }
                    else
                    {
                        var getArrayModel = await _supabaseClient.From<Userdto>()
                         .Where(n => n.Id == recp && n.Deleted == false)
                         .Single();
                        //var getArrayModel = getArray.Models.FirstOrDefault();
                        if (getArrayModel.MessagePayload == null)
                        {
                            List<PayLoad> emptyProfilePic = [];
                            emptyProfilePic.Add(payLoad);
                            string jsonListEmpty = JsonConvert.SerializeObject(emptyProfilePic);
                            getArrayModel.MessagePayload = jsonListEmpty;
                            await getArrayModel.Update<Userdto>();
                            Console.WriteLine("ADded empty");
                            return Ok("Added to an empty array");
                        }


                        List<PayLoad> allProfilePic = JsonConvert.DeserializeObject<List<PayLoad>>(getArrayModel.MessagePayload);

                        allProfilePic.Add(payLoad);



                        string jsonList = JsonConvert.SerializeObject(allProfilePic);


                        getArrayModel.MessagePayload = jsonList;

                        await getArrayModel.Update<Userdto>();
                        Console.WriteLine("aDDed Payload");

                        // Store payload for inactive recipient
                        Console.WriteLine($"Inserted to Stored for userId:{recp}");
                        //await StorePayloadForUserAsync(recp, payLoad);
                    }
                    //await _hubContext.Clients.Group(recp.ToString()).SendAsync("ReceiveMessage", payLoad);
                    if (_userConnectionManager.TryGetValue(sender.ToString(), out var userInfo1) && userInfo1.IsActive)
                    {
                        // Send message to active recipient
                        await _hubContext.Clients.Group(sender.ToString()).SendAsync("ReceiveMessage", payLoad);

                    }
                    else
                    {
                        var getArrayModel = await _supabaseClient.From<Userdto>()
                         .Where(n => n.Id == sender && n.Deleted == false)
                         .Single();
                        //var getArrayModel = getArray.Models.FirstOrDefault();
                        if (getArrayModel.MessagePayload == null)
                        {
                            List<PayLoad> emptyProfilePic = [];
                            emptyProfilePic.Add(payLoad);
                            string jsonListEmpty = JsonConvert.SerializeObject(emptyProfilePic);
                            getArrayModel.MessagePayload = jsonListEmpty;
                            await getArrayModel.Update<Userdto>();
                            Console.WriteLine("ADded empty");
                            return Ok("Added to an empty array");
                        }


                        List<PayLoad> allProfilePic = JsonConvert.DeserializeObject<List<PayLoad>>(getArrayModel.MessagePayload);

                        allProfilePic.Add(payLoad);



                        string jsonList = JsonConvert.SerializeObject(allProfilePic);


                        getArrayModel.MessagePayload = jsonList;

                        await getArrayModel.Update<Userdto>();
                        Console.WriteLine("aDDed Payload");

                        // Store payload for inactive recipient
                        Console.WriteLine($"Inserted to Stored for userId:{recp}");
                        //await StorePayloadForUserAsync(recp, payLoad);
                        Console.WriteLine($"Insert about to Stored for userId:{recp}");

                        //await StorePayloadForUserAsync(recp, payLoad);
                    }
                    //await _hubContext.Clients.Group(sender.ToString()).SendAsync("ReceiveMessage", payLoad);


                }

            }
            else if (payLoad.type == "UPDATE" && payLoad.record.Deleted == true)
            {
                
                var final = payLoad.record.Deleteer;
                var recp = payLoad.record.RecpientId;
                var sender = payLoad.record.SenderId;

                if (final == recp)
                {
                    final = sender;
                }
                else
                {
                    final = recp;
                }
                if (recp != sender)
                {
                    if (_userConnectionManager.TryGetValue(final.ToString(), out var userInfo) && userInfo.IsActive)
                    {
                        // Send message to active recipient
                        await _hubContext.Clients.Group(final.ToString()).SendAsync("ReceiveMessage", payLoad);

                    }
                    else
                    {
                        var getArrayModel = await _supabaseClient.From<Userdto>()
                         .Where(n => n.Id == final && n.Deleted == false)
                         .Single();
                        //var getArrayModel = getArray.Models.FirstOrDefault();
                        if (getArrayModel.MessagePayload == null)
                        {
                            List<PayLoad> emptyProfilePic = [];
                            emptyProfilePic.Add(payLoad);
                            string jsonListEmpty = JsonConvert.SerializeObject(emptyProfilePic);
                            getArrayModel.MessagePayload = jsonListEmpty;
                            await getArrayModel.Update<Userdto>();
                            Console.WriteLine("ADded empty");
                            return Ok("Added to an empty array");
                        }


                        List<PayLoad> allProfilePic = JsonConvert.DeserializeObject<List<PayLoad>>(getArrayModel.MessagePayload);

                        allProfilePic.Add(payLoad);



                        string jsonList = JsonConvert.SerializeObject(allProfilePic);


                        getArrayModel.MessagePayload = jsonList;

                        await getArrayModel.Update<Userdto>();
                        Console.WriteLine("aDDed Payload");

                        // Store payload for inactive recipient
                        Console.WriteLine($"Inserted to Stored for userId:{recp}");
                        //await StorePayloadForUserAsync(recp, payLoad);
                        Console.WriteLine($"Insert about to Stored for userId:{recp}");

                        //await StorePayloadForUserAsync(recp, payLoad);
                    }
                    //await _hubContext.Clients.Group(final.ToString()).SendAsync("ReceiveMessage", payLoad);

                }


            }
            else
            {
                Console.WriteLine("Problem, Message neither Upd,Del,Ins");
            }          

            return Ok();
        }

        [HttpPost("wsc")]
        public async Task<IActionResult> HandleConversationEvent([FromBody] object payloadObject)
        {
            if (payloadObject == null)
            {
                Console.WriteLine("1");

                return BadRequest("Payload is null");
            }
            // Cast the payload to a JObject
            string x = payloadObject.ToString();

            ConvPayLoad convPayLoad = JsonConvert.DeserializeObject<ConvPayLoad>(x);

            foreach (var user in _userConnectionManager.GetAllUsers())
            {
                if (user.Value.IsActive)
                {
                    Console.WriteLine("2");

                    await _hubContext.Clients.Client(user.Value.ConnectionId).SendAsync("Receive Conversation", convPayLoad);
                }
                else
                {
                    Console.WriteLine("3");
                    long longConv = long.Parse(user.Key);
                    var getArrayModel = await _supabaseClient.From<Userdto>()
                     .Where(n => n.Id == longConv && n.Deleted == false)
                     .Single();
                    Console.WriteLine("4");

                    //var getArrayModel = getArray.Models.FirstOrDefault();
                    if (getArrayModel.ConvPayload == null)
                    {
                        Console.WriteLine("5");

                        List<ConvPayLoad> emptyProfilePic = [];
                        emptyProfilePic.Add(convPayLoad);
                        string jsonListEmpty = JsonConvert.SerializeObject(emptyProfilePic);
                        getArrayModel.ConvPayload = jsonListEmpty;
                        await getArrayModel.Update<Userdto>();
                        Console.WriteLine("6");

                        Console.WriteLine("ADded empty");
                        return Ok("Added to an empty array");
                    }

                    Console.WriteLine("7");

                    List<ConvPayLoad> allProfilePic = JsonConvert.DeserializeObject<List<ConvPayLoad>>(getArrayModel.ConvPayload);

                    allProfilePic.Add(convPayLoad);



                    string jsonList = JsonConvert.SerializeObject(allProfilePic);


                    getArrayModel.ConvPayload = jsonList;
                    Console.WriteLine("8");

                    await getArrayModel.Update<Userdto>();
                    Console.WriteLine("aDDed Payload");

                    // Store payload for inactive recipient
                    //Console.WriteLine($"Inserted to Stored for userId:{recp}");
                    //await StorePayloadForUserAsync(recp, payLoad);
                }
            }
            return Ok();

        } 

        [HttpPost("WebSocketUser")]
        public async Task<IActionResult> HandleUserEvent([FromBody] object payloadObject)
        {
            
            if (payloadObject == null)
            {
                Console.WriteLine("the paramerter returned is Null");

                return BadRequest("Payload is null");
            }
            // Cast the payload to a JObject
            string x = payloadObject.ToString();
            UserPayLoad userPayLoad = JsonConvert.DeserializeObject<UserPayLoad>(x);
            

            if (userPayLoad.type == "UPDATE")
            {
                    foreach (var user in _userConnectionManager.GetAllUsers())
                    {
                        if (user.Value.IsActive)
                        {
                        
                        await _hubContext.Clients.Client(user.Value.ConnectionId).SendAsync("Receive UserProfile", userPayLoad);
                        }
                        else
                        {
                        long longUserProfile = long.Parse(user.Key);
                        var getArrayModel = await _supabaseClient.From<Userdto>()
                           .Where(n => n.Id == longUserProfile && n.Deleted == false)
                           .Single();
                        
                        //var getArrayModel = getArray.Models.FirstOrDefault();
                        if (getArrayModel.UserPayload == null)
                        {
                            
                            List<UserPayLoad> emptyProfilePic = [];
                            emptyProfilePic.Add(userPayLoad);
                            string jsonListEmpty = JsonConvert.SerializeObject(emptyProfilePic);
                            getArrayModel.UserPayload = jsonListEmpty;
                            await getArrayModel.Update<Userdto>();
                            return Ok("Added to an empty array");
                        }

                        
                        List<UserPayLoad> allProfilePic = JsonConvert.DeserializeObject<List<UserPayLoad>>(getArrayModel.UserPayload);

                        allProfilePic.Add(userPayLoad);



                        string jsonList = JsonConvert.SerializeObject(allProfilePic);


                        getArrayModel.UserPayload = jsonList;
                        
                        await getArrayModel.Update<Userdto>();
                        
                        // Store payload for inactive recipient
                        //Console.WriteLine($"Inserted to Stored for userId:{recp}");
                        //await StorePayloadForUserAsync(recp, payLoad);
                        //StorePayloadForUser(user.Key, userPayLoad);
                    }
                }
                  
                return Ok();
                
            }
            return BadRequest();
        }

        [HttpPut("zeroNotificationMID"), Authorize]
        public async Task<IActionResult> ZeroNotificationConv([FromQuery] long messageId)
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
                var responseMessage = await _supabaseClient.From<MessageDto>()
                    .Where(n => n.Id == messageId && n.Deleted == false)
                    .Get();
                var SenderMessage = responseMessage.Models.FirstOrDefault();

                if (SenderMessage == null)
                {
                    Console.WriteLine("SenderMessage is null MESSAGE");
                    return StatusCode(11, "Message not found");
                }
                SenderMessage.New = false;
                    await _supabaseClient.From<MessageDto>().Update(SenderMessage);
                    Console.WriteLine("ZeroNotification DONE MESSAGE");

                    return Ok();
                      

               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return BadRequest("Connection Problem, Check if MessageId is valid");
            }

        }

        [HttpPut("zeroNotificationCID"), Authorize]
        public async Task<IActionResult> ZeroNotificationSingle([FromQuery] long convIdPara)
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
                Console.WriteLine($"ZeroNotification Called with a  convId {convIdPara}");
                var responseMessage = await _supabaseClient.From<MessageDto>()
                    .Where(n => n.ConvId == convIdPara && n.Deleted == false)
                    .Get();

                var allmessArray = responseMessage.Models.ToList();
                if (allmessArray.Any())
                {
                    Console.WriteLine("NEW TO FALSE IN BUNCH");
                    // Create a list of updated messages
                    var updatedMessages12 = allmessArray.Select(message => { message.New = false; return message; }).ToList();

                    // Batch update all messages
                    await _supabaseClient.From<MessageDto>().Upsert(updatedMessages12);
                    return Ok();
                }
                
                return Ok();          

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return BadRequest("Connection Problem, Check if MessageId is valid");
            }

        }
        
        [HttpPost("SendMessage"), Authorize]
        public async Task<IActionResult> SendMessage([FromBody] MessageUser messageUser)
        {
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == "Email");
            if (emailClaim == null)
            {
                return Unauthorized("Invalid Token");
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
                    return Unauthorized("Invalid Token");
                }
                
            // Decision: NOT A New Messsage #############
            if (messageUser.ConversationId != 0 )
            {
                
                MessageDto newMessageIf = new MessageDto
                {
                    TimeStamp = DateTime.UtcNow,
                    MessageType = messageUser.MessageType,
                    Status = "Sent",
                    SenderId = Sender.Id,
                    RecpientId = messageUser.RecpientId,
                    Content = messageUser.Content,
                    ConvId = messageUser.ConversationId,
                    IsAudio = messageUser.IsAudio,
                    IsImage = messageUser.IsImage,
                    New = Sender.Id != messageUser.RecpientId ? true : false
                    Reply = messageUser.Reply
                };
                var insertResposne1 = await _supabaseClient.From<MessageDto>().Insert(newMessageIf);
                var messageResposneIf1 = insertResposne1.Models.FirstOrDefault();


                var updateConvTable1 = await _supabaseClient.From<ConversationDto>()
                        .Where(n => n.ConvId == messageUser.ConversationId)
                        .Single();
                            updateConvTable1.LastMessage = messageResposneIf1.Id;
                            updateConvTable1.UpdatedTime = messageResposneIf1.TimeStamp;

                await updateConvTable1.Update<ConversationDto>();
                
                return Ok(messageResposneIf1);
            }
            
            // Decision: New Messsage #############

                
                ConversationDto newConversation = new ConversationDto
                {
                    CreationTime = DateTime.UtcNow,
                    UpdatedTime = DateTime.UtcNow,
                    LastMessage = 346
                };

                var newConvTable = await _supabaseClient.From<ConversationDto>().Insert(newConversation);

                var newConvResponse = newConvTable.Models.FirstOrDefault(); 

                
                // Create new Conversation by meddling with Message,Conversation and Participation Table

                MessageDto newMessage = new MessageDto
                {
                    TimeStamp = DateTime.UtcNow,
                    MessageType = messageUser.MessageType,
                    Status = "Sent",
                    SenderId = Sender.Id,
                    RecpientId = messageUser.RecpientId,
                    Content = messageUser.Content,
                    ConvId = newConvResponse.ConvId,
                    IsImage = messageUser.IsImage,
                    IsAudio = messageUser.IsAudio,
                    New = Sender.Id != messageUser.RecpientId ? true : false,
                    Reply = messageUser.Reply
                };
                // Create a new Message Table with a convId of NULL temporarily

                var insertMessage = await _supabaseClient.From<MessageDto>().Insert(newMessage);
                var messageResposne = insertMessage.Models.FirstOrDefault();

                // create a new row in the conversation table
                // update the NULL convId back
                // Console.WriteLine("Updating Last Message id in Conv table ");


                var responseUpdateMessageId = await _supabaseClient.From<ConversationDto>()
                    .Where(n => n.ConvId == newConvResponse.ConvId)
                    .Single();

                responseUpdateMessageId.LastMessage = messageResposne.Id;
                await responseUpdateMessageId.Update<ConversationDto>();

                                                
                //Console.WriteLine("Updated last Message Id in conversation Table");

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
                //Console.WriteLine("Created Participants");
                
                var responseUpdateMessagefinal = await _supabaseClient.From<MessageDto>()
                        .Where(n => n.Id == messageResposne.Id && n.Deleted == false)
                        .Get();
                var finalBound = responseUpdateMessagefinal.Models.FirstOrDefault();

                 
                //long convIdJson = newConvResponse.ConvId;
                
                return Ok(finalBound);
                                        
                                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
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
                updateMessage.Edited = true;
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
        public async Task<IActionResult> DeleteMessage([FromQuery] long deleteMessage)
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
                        .Where(n => n.Id == deleteMessage && n.Deleted == false)
                        .Get();
                Console.WriteLine("Got the message");
                    var getRecpientId = getMessageInfo.Models.FirstOrDefault();
                    
                    if (getRecpientId == null)
                    {
                        return BadRequest("Problem with the parameter Id");
                    }
                long realRecpId = getRecpientId.RecpientId;
                if (getRecpientId.RecpientId == Sender.Id)
                {
                    realRecpId = getRecpientId.SenderId;
                }
                var messageQuery = await _supabaseClient.From<MessageDto>()
                    .Where(n => ((n.SenderId == Sender.Id && n.RecpientId == realRecpId)))
                    .Where(n => (n.Deleted == false))
                    .Get();


                var anotherMessageQuery = await _supabaseClient.From<MessageDto>()
                    .Where(n => ((n.SenderId == realRecpId && n.RecpientId == Sender.Id)))
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
                                .Where(n => n.Id == deleteMessage)
                                .Single();
                            up.Deleted = true;
                            up.Deleteer = Sender.Id;
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


                if (deleteMessage == getconvId.LastMessage)
                        {
                    Console.WriteLine("Correct Message Id");

                    // First part: where Sender is Sender.Id and Recipient is getRecpientId.RecpientId
                    var messagesSent = await _supabaseClient.From<MessageDto>()
                        .Where(n => n.SenderId == Sender.Id && n.RecpientId == realRecpId)
                        .Where(n => n.Deleted == false)
                        .Get();

                    // Second part: where Sender is getRecpientId.RecpientId and Recipient is Sender.Id
                    var messagesReceived = await _supabaseClient.From<MessageDto>()
                        .Where(n => n.SenderId == realRecpId && n.RecpientId == Sender.Id)
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
                    .Where(n => n.Id == deleteMessage)
                    .Single();
                antoherHey.Deleted = true;
                antoherHey.Deleteer = Sender.Id;

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
       
        [HttpGet("GetConversationMessage"), Authorize] 
        public async Task<IActionResult> GetConversationMessage([FromQuery] long query)
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
                        .Where(n => n.ConvId == query && n.Deleted==false)
                        .Where(n=> n.SenderId == hey.Id || n.RecpientId == hey.Id)
                        .Order(n => n.TimeStamp, Constants.Ordering.Ascending)
                        .Get();
                var allmessArray = convMessage.Models.ToList();
                if (allmessArray.Any())
                {

                    var alvarez = await _supabaseClient.From<MessageDto>()
                        .Where(n => n.ConvId == query && n.Deleted == false)
                        .Where(n => n.RecpientId == hey.Id && n.New == true)
                        .Get();
                     var messagesToUpdateList = alvarez.Models.ToList();

                    
                foreach (var message in messagesToUpdateList)
                {
                    message.New = false;
                    await _supabaseClient.From<MessageDto>().Update(message);
                }
                    var convMessage100 = await _supabaseClient.From<MessageDto>()
                            .Where(n => n.ConvId == query && n.Deleted == false)
                            .Where(n => n.SenderId == hey.Id || n.RecpientId == hey.Id)
                            .Order(n => n.TimeStamp, Constants.Ordering.Ascending)
                            .Get();
                    var allmessArray100 = convMessage100.Models.ToList();
                    // var updatedMessages12 = allmessArray.Select(message => { message.New = false; return message; }).ToList();

                    // Batch update all messages
                    //await _supabaseClient.From<MessageDto>().Upsert(updatedMessages12);

                    return Ok(allmessArray100);
                }
                return Ok("Array empty");
                 
                
            }
            catch (Exception)
            {
                return BadRequest("Connection Problem first part");
            }
        }
        
        [HttpGet("GetLastMessage"), Authorize]
        public async Task<IActionResult> GetLastMessage([FromQuery] long query)
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
                        .Where(n => n.ConvId == query && n.Deleted == false)
                        .Where(n => n.SenderId == hey.Id || n.RecpientId == hey.Id)
                        .Order(n => n.TimeStamp, Constants.Ordering.Ascending)
                        .Get();
                
                var allmessArray = convMessage.Models.LastOrDefault();
                if (allmessArray == null)
                {
                    return Ok(null);
                }
                
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
                    return Unauthorized("Invalid Token");
                }
                var responseUpdate = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Id == hey.Id)
                            .Single();
                responseUpdate.MessagePayload = null;
                responseUpdate.ConvPayload = null;
                responseUpdate.UserPayload = null;
                
                await responseUpdate.Update<Userdto>();

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
                        .Where(n => n.Id == messageId && n.Deleted == false)
                        .Get();
                    var getContent = messResponse.Models.FirstOrDefault();
                    string content = getContent.Content;
                    bool isaudio = getContent.IsAudio;
                    bool isimage = getContent.IsImage;
                    long LastMessageId = getContent.Id;
                    bool seenUnseen = getContent.New;
                    long messageSender200 = getContent.SenderId;
                    var messResponseNoti = await _supabaseClient.From<MessageDto>()
                       .Where(n => n.ConvId == convId && n.Deleted == false)
                       .Where(n=>n.RecpientId==hey.Id && n.New == true)
                       .Get();
                    var getContentNoti = messResponseNoti.Models.Count;
                    //Console.WriteLine($"Notification for {content} {getContentNoti}");

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
                        
                       
                    }
                    else if (first.UserId != hey.Id)
                    {
                        UserId = first.UserId;
                    }
                    else if (second.UserId!=hey.Id) {
                        UserId = second.UserId;
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
                    
                    // Get user Email
                    var getConvEmail = await _supabaseClient.From<Userdto>()
                        .Where(n=>n.Id == UserId)
                        .Get();
                    var getEmail = getConvEmail.Models.FirstOrDefault();

                    
                    List<string> allProfilePic = getProfile2.ProfilePic != null
                    ? JsonConvert.DeserializeObject<List<string>>(getProfile2.ProfilePic)
                    : null;
                    //allProfilePic.Reverse();
                    if (allProfilePic != null)
                    {
                        allProfilePic.Reverse();
                       // Console.WriteLine("REversed HHHH");
                    }
                    

                    CustomConv xz = new CustomConv
                    {
                        UserName = userName,
                        UpdatedTime = messageIdConv.UpdatedTime,
                        Message = content,
                        Seen = seenUnseen,
                        UserId = UserId,
                        ConvId = convId,
                        LastName = getProfile2.LastName,
                        MessageId = LastMessageId,
                        Status = getEmail.Status,
                        NotificationCount= getContentNoti,
                        LastSeen = getEmail.LastSeen,
                        Bio = getProfile2.Bio,
                        Email = getEmail.Email,
                        IsAudio= isaudio,
                        IsImage = isimage,
                        ProfilePicConv= allProfilePic,
                        MessageSender = messageSender200,
                        Deleted = getProfile2.Deleted
                    }; 
                        allyouNeed.Add(xz);
                    }
                    var orderedResults = allyouNeed.OrderByDescending(n => n.UpdatedTime).ToList();

                 return Ok(orderedResults);
                                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return StatusCode(500,"Connection Problem, Backend"); 
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
                //Console.WriteLine("DELETE CONVERSATION");
                //Console.WriteLine(deleteConv);
                await _supabaseClient.From<ParticipantDto>()
                        .Where(n => n.ConversationId == deleteConv.ConvId)
                        .Delete();
                //Console.WriteLine("PartCIPANT");
                await _supabaseClient.From<ConversationDto>()
                    .Where(n => n.ConvId == deleteConv.ConvId)
                    .Delete();

                
                var up1 = await _supabaseClient.From<MessageDto>()
                     .Where(n => n.ConvId == deleteConv.ConvId && n.Deleted==false)
                     .Get();
                var up = up1.Models.ToList();
                /*
                foreach (var message in up)
                {
                    message.Deleted = true;
                    await _supabaseClient.From<MessageDto>()
                                         .Where(m => m.Id == message.Id)
                                         .Update(message);
                }
                */
                if (up.Any())
                {
                    // Create a list of updated messages
                    var updatedMessages = up.Select(message => { message.Deleted = true; return message; }).ToList();
                   
                    // Batch update all messages
                    await _supabaseClient.From<MessageDto>().Upsert(updatedMessages);
              
                }

                

                return Ok("Successfully Deleted");
                
            }
            catch
            {
                return BadRequest("Connection Problem");
            }
        }
    }
}