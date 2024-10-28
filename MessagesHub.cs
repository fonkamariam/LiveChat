using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using Supabase;
using Supabase.Interfaces;
using Microsoft.Extensions.Configuration;
using LiveChat.Models;
using Newtonsoft.Json;

[Authorize]
public class MessagesHub : Hub
{
	private static int _connectedClients = 0;
    private readonly Supabase.Client _supabaseClient;
    private readonly IConfiguration _configuration;
    public MessagesHub(IConfiguration configuration, Client supabaseClient)
    {
        _configuration = configuration;
        _supabaseClient = supabaseClient;
        
    }
    // Dictionary to track users who are logging out manually
    private static Dictionary<long, bool> userLogoutFlags = new Dictionary<long, bool>();
    // for tracking connected users when sending data
    private readonly Dictionary<long, string> _connectedUsers = new();



    private Task<List<long>> GetConnectedUserIds()
    {
        // Return the list of user IDs that are currently connected
        return Task.FromResult(_connectedUsers.Keys.ToList());
    }

    
    public override async Task OnConnectedAsync()
    {
        Console.WriteLine("Connected Task Invoked");
        
        var userIdclaim = Context.User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdclaim == null)
        {
            Console.WriteLine("User Id is Null");
            return;
        }
        var userId = userIdclaim.Value.Split(':')[0].Trim();
        long userIdLong = long.Parse(userId);
        DateTime dateTime = DateTime.UtcNow;
        
        //Add them to the SignalR connection pool
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);

         // Add the connection ID to the dictionary for the user
        _connectedUsers[userIdLong] = Context.ConnectionId;
        
        var logoutHandle = await _supabaseClient.From<Userdto>()
         .Where(n => n.Id == userIdLong)
         .Single();
        logoutHandle.Status = "true";
        logoutHandle.LastSeen = dateTime;
        logoutHandle.Active = true;
        await logoutHandle.Update<Userdto>();
        
        ////
            var newUserStatus = new UserStatus
            {
                Type = "UserStatus",
                UserId = userIdLong,
                Status = true, 
                Time = dateTime
            };
            // Sending the status to all connected users
            var allConnectedUsers = await GetConnectedUserIds(); // Implement this method to get all connected user IDs
            foreach (var recipientId in allConnectedUsers)
            {
                if (recipientId != userIdLong) // Don't send to the user who just disconnected
                {
                    try
                    {
                        await Clients.User(recipientId.ToString()).SendAsync("UserStatusChanged", userIdLong, true, dateTime);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending user status to {recipientId}: Storing in thier db...");

                        // Retrieve the recipient's existing missed payload from the database
                        var recipientData = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Id == recipientId && n.Deleted == false)
                            .Single();

                        List<UserStatus> messageList;

                        // If the recipient's message payload is null, create a new list
                        if (recipientData.OnlinePayload == null)
                        {
                            messageList = new List<UserStatus>();
                        }
                        else
                        {
                            // Deserialize the existing message list
                            messageList = JsonConvert.DeserializeObject<List<UserStatus>>(recipientData.OnlinePayload);
                        }

                        // Check if there's already a UserStatus object for this userIdLong
                        var existingStatus = messageList.FirstOrDefault(m => m.Type == "UserStatus" && m.UserId == userIdLong);

                        // If it exists, update its status
                        if (existingStatus != null)
                        {
                            existingStatus.Status = newUserStatus.Status;
                            existingStatus.Time = newUserStatus.Time;
                        }
                        else
                        {
                            // Otherwise, add the new UserStatus object to the list
                            messageList.Add(newUserStatus);
                        }

                        // Serialize the updated list
                        string updatedMessageList = JsonConvert.SerializeObject(messageList);

                        // Store the updated message list back into the recipient's database
                        recipientData.OnlinePayload = updatedMessageList;
                        await recipientData.Update<Userdto>();

                    }
                }
            }
               
        await base.OnConnectedAsync();
    }

	public override async Task OnDisconnectedAsync(Exception exception)
	{
        Console.WriteLine("Disconnected Invoked");
        
        var userIdclaim = Context.User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdclaim == null)
        {
            Console.WriteLine("Email is Null");
            return;
        }
        var userId = userIdclaim.Value.Split(':')[0].Trim();
        long userIdLong = long.Parse(userId);
        DateTime dateTime = DateTime.UtcNow;
        // Check if this disconnection is due to manual logout
        if (userLogoutFlags.ContainsKey(userIdLong) && userLogoutFlags[userIdLong])
        {
            // This is a manual logout, so skip the disconnection logic
            Console.WriteLine($"User {userIdLong} logged out manually. Skipping OnDisconnectedAsync.");
            userLogoutFlags.Remove(userIdLong);
            return;
        }

        var logoutHandle = await _supabaseClient.From<Userdto>()
                 .Where(n => n.Id == userIdLong)
                 .Single();
        logoutHandle.Status = "false";
        logoutHandle.LastSeen = dateTime;
        logoutHandle.Active = false;
        await logoutHandle.Update<Userdto>();
        
        // Remove from the SingalR connection pool
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
        // Remove the connection ID from the dictionary for the user
        _connectedUsers.Remove(userIdLong); 


        
            var newUserStatus = new UserStatus
            {
                Type = "UserStatus",
                UserId = userIdLong,
                Status = false, 
                Time = dateTime
            };
            // Sending the status to all connected users
            var allConnectedUsers = await GetConnectedUserIds(); // Implement this method to get all connected user IDs
            foreach (var recipientId in allConnectedUsers)
            {
                if (recipientId != userIdLong) // Don't send to the user who just disconnected
                {
                    try
                    {
                        await Clients.User(recipientId.ToString()).SendAsync("UserStatusChanged", userIdLong, false, dateTime);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending user status to {recipientId}: Storing in thier db...");

                        // Retrieve the recipient's existing missed payload from the database
                        var recipientData = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Id == recipientId && n.Deleted == false)
                            .Single();

                        List<UserStatus> messageList;

                        // If the recipient's message payload is null, create a new list
                        if (recipientData.OnlinePayload == null)
                        {
                            messageList = new List<UserStatus>();
                        }
                        else
                        {
                            // Deserialize the existing message list
                            messageList = JsonConvert.DeserializeObject<List<UserStatus>>(recipientData.OnlinePayload);
                        }

                        // Check if there's already a UserStatus object for this userIdLong
                        var existingStatus = messageList.FirstOrDefault(m => m.Type == "UserStatus" && m.UserId == userIdLong);

                        // If it exists, update its status
                        if (existingStatus != null)
                        {
                            existingStatus.Status = newUserStatus.Status;
                            existingStatus.Time = newUserStatus.Time;
                        }
                        else
                        {
                            // Otherwise, add the new UserStatus object to the list
                            messageList.Add(newUserStatus);
                        }

                        // Serialize the updated list
                        string updatedMessageList = JsonConvert.SerializeObject(messageList);

                        // Store the updated message list back into the recipient's database
                        recipientData.OnlinePayload = updatedMessageList;
                        await recipientData.Update<Userdto>();

                    }
                }
            }
            

        await base.OnDisconnectedAsync(exception);
    }

	public static int GetConnectedClients()
	{
		return _connectedClients;
	}

	public async Task SendMessage(string user, string message)
	{

		await Clients.All.SendAsync("ReceiveMessage", user, message);
		return;
	}

    public async Task VisibilityChanged(string state,long userIdLong)
    {
        Console.WriteLine("Visblity Changed Task Invoked");
        
        if (state == "hidden")
        {
           DateTime dateTime = DateTime.UtcNow;
           var logoutHandle = await _supabaseClient.From<Userdto>()
                .Where(n => n.Id == userIdLong && n.Deleted == false)
                .Single();
                logoutHandle.Status = "false";
                logoutHandle.LastSeen = dateTime;

            await logoutHandle.Update<Userdto>();
        
            var newUserStatus = new UserStatus
            {
                Type = "UserStatus",
                UserId = userIdLong,
                Status = false, 
                Time = dateTime
            };
            // Sending the status to all connected users
            var allConnectedUsers = await GetConnectedUserIds(); // Implement this method to get all connected user IDs
            foreach (var recipientId in allConnectedUsers)
            {
                if (recipientId != userIdLong) // Don't send to the user who just disconnected
                {
                    try
                    {
                        await Clients.User(recipientId.ToString()).SendAsync("UserStatusChanged", userIdLong, false, dateTime);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending user status to {recipientId}: Storing in thier db...");

                        // Retrieve the recipient's existing missed payload from the database
                        var recipientData = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Id == recipientId && n.Deleted == false)
                            .Single();

                        List<UserStatus> messageList;

                        // If the recipient's message payload is null, create a new list
                        if (recipientData.OnlinePayload == null)
                        {
                            messageList = new List<UserStatus>();
                        }
                        else
                        {
                            // Deserialize the existing message list
                            messageList = JsonConvert.DeserializeObject<List<UserStatus>>(recipientData.OnlinePayload);
                        }

                        // Check if there's already a UserStatus object for this userIdLong
                        var existingStatus = messageList.FirstOrDefault(m => m.Type == "UserStatus" && m.UserId == userIdLong);

                        // If it exists, update its status
                        if (existingStatus != null)
                        {
                            existingStatus.Status = newUserStatus.Status;
                            existingStatus.Time = newUserStatus.Time;
                        }
                        else
                        { 
                            messageList.Add(newUserStatus);
                        }

                        // Serialize the updated list
                        string updatedMessageList = JsonConvert.SerializeObject(messageList);

                        // Store the updated message list back into the recipient's database
                        recipientData.OnlinePayload = updatedMessageList;
                        await recipientData.Update<Userdto>();

                    }
                }
            }
           
            
                
        }
        else
        {
            DateTime dateTime = DateTime.UtcNow;
            
            var logoutHandle = await _supabaseClient.From<Userdto>()
                .Where(n => n.Id == userIdLong && n.Deleted == false)
                .Single();
                logoutHandle.Status = "true";
                logoutHandle.LastSeen = dateTime;

            await logoutHandle.Update<Userdto>();
            var newUserStatus = new UserStatus
            {
                Type = "UserStatus",
                UserId = userIdLong,
                Status = true, 
                Time = dateTime
            };
            // Sending the status to all connected users
            var allConnectedUsers = await GetConnectedUserIds(); // Implement this method to get all connected user IDs
            foreach (var recipientId in allConnectedUsers)
            {
                if (recipientId != userIdLong) // Don't send to the user who just disconnected
                {
                    try
                    {
                        await Clients.User(recipientId.ToString()).SendAsync("UserStatusChanged", userIdLong, true, dateTime);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending user status to {recipientId}: Storing in thier db...");

                        // Retrieve the recipient's existing missed payload from the database
                        var recipientData = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Id == recipientId && n.Deleted == false)
                            .Single();

                        List<UserStatus> messageList;

                        // If the recipient's message payload is null, create a new list
                        if (recipientData.OnlinePayload == null)
                        {
                            messageList = new List<UserStatus>();
                        }
                        else
                        {
                            // Deserialize the existing message list
                            messageList = JsonConvert.DeserializeObject<List<UserStatus>>(recipientData.OnlinePayload);
                        }

                        // Check if there's already a UserStatus object for this userIdLong
                        var existingStatus = messageList.FirstOrDefault(m => m.Type == "UserStatus" && m.UserId == userIdLong);

                        // If it exists, update its status
                        if (existingStatus != null)
                        {
                            existingStatus.Status = newUserStatus.Status;
                            existingStatus.Time = newUserStatus.Time;
                        }
                        else
                        {
                            // Otherwise, add the new UserStatus object to the list
                            messageList.Add(newUserStatus);
                        }

                        // Serialize the updated list
                        string updatedMessageList = JsonConvert.SerializeObject(messageList);

                        // Store the updated message list back into the recipient's database
                        recipientData.OnlinePayload = updatedMessageList;
                        await recipientData.Update<Userdto>();

                    }
                }
            }
            
        }
    }

    public class UserStatus
    {
        public string Type { get; set; }
        public long UserId { get; set; }
        public bool Status { get; set; }
        public DateTime Time {get; set;}
    }
   
    public async Task UserLoggingOutTask(long userIdLong)
    {
        // Step 1: Update user status in the database
        Console.WriteLine("UserLoggedOut Tak Invoked");
        
        var logoutHandle = await _supabaseClient.From<Userdto>()
            .Where(n => n.Id == userIdLong && n.Deleted == false)
            .Single();
        logoutHandle.Status = "false";
        DateTime dateTime = DateTime.UtcNow;
        logoutHandle.LastSeen = dateTime;
        logoutHandle.Active = false;
        await logoutHandle.Update<Userdto>();
        // for disconnection method not to run
        userLogoutFlags[userIdLong] = true;
        // Step 2: Remove the user from the SignalR group
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userIdLong.ToString());

        var newUserStatus = new UserStatus
            {
                Type = "UserStatus",
                UserId = userIdLong,
                Status = false, 
                Time = dateTime
            };
            // Sending the status to all connected users
            var allConnectedUsers = await GetConnectedUserIds(); // Implement this method to get all connected user IDs
            foreach (var recipientId in allConnectedUsers)
            {
                if (recipientId != userIdLong) // Don't send to the user who just disconnected
                {
                    try
                    {
                        await Clients.User(recipientId.ToString()).SendAsync("UserStatusChanged", userIdLong, false, dateTime);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending user status to {recipientId}: Storing in thier db...");

                        // Retrieve the recipient's existing missed payload from the database
                        var recipientData = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Id == recipientId && n.Deleted == false)
                            .Single();

                        List<UserStatus> messageList;

                        // If the recipient's message payload is null, create a new list
                        if (recipientData.OnlinePayload == null)
                        {
                            messageList = new List<UserStatus>();
                        }
                        else
                        {
                            // Deserialize the existing message list
                            messageList = JsonConvert.DeserializeObject<List<UserStatus>>(recipientData.OnlinePayload);
                        }

                        // Check if there's already a UserStatus object for this userIdLong
                        var existingStatus = messageList.FirstOrDefault(m => m.Type == "UserStatus" && m.UserId == userIdLong);

                        // If it exists, update its status
                        if (existingStatus != null)
                        {
                            existingStatus.Status = newUserStatus.Status;
                            existingStatus.Time = newUserStatus.Time;
                        }
                        else
                        { 
                            messageList.Add(newUserStatus);
                        }

                        // Serialize the updated list
                        string updatedMessageList = JsonConvert.SerializeObject(messageList);

                        // Store the updated message list back into the recipient's database
                        recipientData.OnlinePayload = updatedMessageList;
                        await recipientData.Update<Userdto>();

                    }
                }
            }

        
    }

    public async Task TypingIndicator(long idPara,bool valuePara)
    {
        var userIdclaim = Context.User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdclaim == null) return;

        var userId = userIdclaim.Value.Split(':')[0].Trim();
        long userIdLong = long.Parse(userId);
        
        await Clients.Group(idPara.ToString()).SendAsync("Typing", userIdLong,valuePara);

    }
    // Project "Make Fast"
    
    public async Task HandleSendMessageTask(long recipient, MessageDto messageObject)
    {
        
        try
        {
            await Clients.Group(recipient.ToString()).SendAsync("ReceiveSendMessage", messageObject);
        }
        catch (Exception ex)
        {
            // Handle specific exceptions based on your requirements
            Console.WriteLine($"Error sending message to {recipient}: Storing in thier db...");
            // Logic to store the message in the database or handle offline users
            // Create a new object with the type "SendMessage" and include the original messageObject
            var newMessageObject = new
            {
                Type = "SendMessage",
                Data = messageObject
            };
            // Serialize the new object
            string serializedMessage = JsonConvert.SerializeObject(newMessageObject);

            // Retrieve the recipient's existing message list from the database
            var recipientData = await _supabaseClient.From<Userdto>()
                .Where(n => n.Id == recipient && n.Deleted == false)
                .Single();

            List<string> messageList;
            
            // If the recipient's message payload is null, create a new list
            if (recipientData.MissedPayload == null)
            {
                messageList = new List<string>();
            }
            else
            {
                // Deserialize the existing message list
                messageList = JsonConvert.DeserializeObject<List<string>>(recipientData.MissedPayload);
            }

            // Add the new serialized message to the list
            messageList.Add(serializedMessage);

            // Serialize the updated list
            string updatedMessageList = JsonConvert.SerializeObject(messageList);

            // Store the updated message list back into the recipient's database
            recipientData.MissedPayload = updatedMessageList;
            await recipientData.Update<Userdto>();

            Console.WriteLine("Message stored for disconnected user.");
        }
    }

    public async Task HandleEditMessageTask(long recipient, MessageDto messageObject)
    {
        
        try
        {
            await Clients.Group(recipient.ToString()).SendAsync("ReceiveEditMessage", messageObject);
        }
        catch (Exception ex)
        {
            // Handle specific exceptions based on your requirements
            Console.WriteLine($"Error sending message to {recipient}: Storing in thier db...");
            // Logic to store the message in the database or handle offline users
            // Create a new object with the type "SendMessage" and include the original messageObject
            var newMessageObject = new
            {
                Type = "EditMessage",
                Data = messageObject
            };
            // Serialize the new object
            string serializedMessage = JsonConvert.SerializeObject(newMessageObject);

            // Retrieve the recipient's existing message list from the database
            var recipientData = await _supabaseClient.From<Userdto>()
                .Where(n => n.Id == recipient && n.Deleted == false)
                .Single();

            List<string> messageList;
            
            // If the recipient's message payload is null, create a new list
            if (recipientData.MissedPayload == null)
            {
                messageList = new List<string>();
            }
            else
            {
                // Deserialize the existing message list
                messageList = JsonConvert.DeserializeObject<List<string>>(recipientData.MissedPayload);
            }

            // Add the new serialized message to the list
            messageList.Add(serializedMessage);

            // Serialize the updated list
            string updatedMessageList = JsonConvert.SerializeObject(messageList);

            // Store the updated message list back into the recipient's database
            recipientData.MissedPayload = updatedMessageList;
            await recipientData.Update<Userdto>();

            Console.WriteLine("Message stored for disconnected user.");
        }

    }

    public async Task HandleDeleteMessageTask(long recipient, long messageId,long convId)
    {
        try
        {
            await Clients.Group(recipient.ToString()).SendAsync("ReceiveDeleteMessage", messageId,convId);
        }
        catch (Exception ex)
        {
            // Create a new object with the type "DeleteMessage" and include the messageId and convId
            var deleteMessageObject = new
            {
                Type = "DeleteMessage",
                MessageId = messageId,
                ConvId = convId
            };

            // Serialize the new delete message object
            string serializedDeleteMessage = JsonConvert.SerializeObject(deleteMessageObject);

            // Retrieve the recipient's existing message list from the database
            var recipientData = await _supabaseClient.From<Userdto>()
                .Where(n => n.Id == recipient && n.Deleted == false)
                .Single();

            List<string> messageList;
            
            // If the recipient's message payload is null, create a new list
            if (recipientData.MissedPayload == null)
            {
                messageList = new List<string>();
            }
            else
            {
                // Deserialize the existing message list
                messageList = JsonConvert.DeserializeObject<List<string>>(recipientData.MissedPayload);
            }

            // Add the new serialized delete message to the list
            messageList.Add(serializedDeleteMessage);

            // Serialize the updated list
            string updatedMessageList = JsonConvert.SerializeObject(messageList);

            // Store the updated message list back into the recipient's database
            recipientData.MissedPayload = updatedMessageList;
            await recipientData.Update<Userdto>();

            Console.WriteLine("Delete message stored for disconnected user.");
        }

        
    }
    
    public async Task HanldeSeenUnseenTask (long recpient,long messageId,long convId) 
    {
        try
        {
            await Clients.Group(recpient.ToString()).SendAsync("ReceiveSeenUnseen", messageId,convId);
        }
        catch (Exception ex)
        {
            // Create a new object with the type "DeleteMessage" and include the messageId and convId
            var seenUnseenObject = new
            {
                Type = "SeenUnseen",
                MessageId = messageId,
                ConvId = convId
            };

            // Serialize the new delete message object
            string serializedDeleteMessage = JsonConvert.SerializeObject(seenUnseenObject);

            // Retrieve the recipient's existing message list from the database
            var recipientData = await _supabaseClient.From<Userdto>()
                .Where(n => n.Id == recpient && n.Deleted == false)
                .Single();

            List<string> messageList;
            
            // If the recipient's message payload is null, create a new list
            if (recipientData.MissedPayload == null)
            {
                messageList = new List<string>();
            }
            else
            {
                // Deserialize the existing message list
                messageList = JsonConvert.DeserializeObject<List<string>>(recipientData.MissedPayload);
            }

            // Add the new serialized delete message to the list
            messageList.Add(serializedDeleteMessage);

            // Serialize the updated list
            string updatedMessageList = JsonConvert.SerializeObject(messageList);

            // Store the updated message list back into the recipient's database
            recipientData.MissedPayload = updatedMessageList;
            await recipientData.Update<Userdto>();

            Console.WriteLine("seenUnseen message stored for disconnected user.");
        }
    }   
    
    public async Task HanldeUserProfileTask (long userIdLong, UserProfileFrontend userProfile) 
    {
        
        // Sending the status to all connected users
        var allConnectedUsers = await GetConnectedUserIds(); // Implement this method to get all connected user IDs
        foreach (var recipientId in allConnectedUsers)
        {
            if (recipientId != userIdLong) // Don't send to the user who just disconnected
            {
                try
                {
                    await Clients.User(recipientId.ToString()).SendAsync("ReceiveUserProfile", userProfile);
                }
                catch (Exception ex)
                {
                    // Handle specific exceptions based on your requirements
                    Console.WriteLine($"Error sending message to {recipientId}: Storing in thier db...");
                    // Logic to store the message in the database or handle offline users
                    // Create a new object with the type "SendMessage" and include the original messageObject
                    var newMessageObject = new
                    {
                        Type = "UserProfile",
                        Data = userProfile
                    };
                    // Serialize the new object
                    string serializedMessage = JsonConvert.SerializeObject(newMessageObject);

                    // Retrieve the recipient's existing message list from the database
                    var recipientData = await _supabaseClient.From<Userdto>()
                        .Where(n => n.Id == recipientId && n.Deleted == false)
                        .Single();

                    List<string> messageList;
                    
                    // If the recipient's message payload is null, create a new list
                    if (recipientData.MissedPayload == null)
                    {
                        messageList = new List<string>();
                    }
                    else
                    {
                        // Deserialize the existing message list
                        messageList = JsonConvert.DeserializeObject<List<string>>(recipientData.MissedPayload);
                    }

                    // Add the new serialized message to the list
                    messageList.Add(serializedMessage);

                    // Serialize the updated list
                    string updatedMessageList = JsonConvert.SerializeObject(messageList);

                    // Store the updated message list back into the recipient's database
                    recipientData.MissedPayload = updatedMessageList;
                    await recipientData.Update<Userdto>();

                    Console.WriteLine("UserProfile stored for disconnected user.");

                }
            }
        }   
        
       
    }
    
    public async Task HandleDeleteConversationTask (long otherUserId,long convId) 
    {
        try
        {
            await Clients.Group(otherUserId.ToString()).SendAsync("ReceiveConversation", otherUserId,convId);
        }
        catch (Exception ex)
        {
            // Create a new object with the type "DeleteMessage" and include the messageId and convId
            var deleteConversationObj = new
            {
                Type = "deleteConversation",
                OtherUserId = otherUserId,
                ConvId = convId
            };

            // Serialize the new delete message object
            string serializedDeleteMessage = JsonConvert.SerializeObject(deleteConversationObj);

            // Retrieve the recipient's existing message list from the database
            var recipientData = await _supabaseClient.From<Userdto>()
                .Where(n => n.Id == otherUserId && n.Deleted == false)
                .Single();

            List<string> messageList;
            
            // If the recipient's message payload is null, create a new list
            if (recipientData.MissedPayload == null)
            {
                messageList = new List<string>();
            }
            else
            {
                // Deserialize the existing message list
                messageList = JsonConvert.DeserializeObject<List<string>>(recipientData.MissedPayload);
            }

            // Add the new serialized delete message to the list
            messageList.Add(serializedDeleteMessage);

            // Serialize the updated list
            string updatedMessageList = JsonConvert.SerializeObject(messageList);

            // Store the updated message list back into the recipient's database
            recipientData.MissedPayload = updatedMessageList;
            await recipientData.Update<Userdto>();

            Console.WriteLine("Delete Conversation stored for disconnected user.");
        }
    }
    public async Task HandleDeletedAccountTask (long userIdLong)
    {
        var deletedAccountObject = new
        {
            Type = "DeletedAccount",
            UserId = userIdLong
        };
        // Sending the status to all connected users
            var allConnectedUsers = await GetConnectedUserIds(); // Implement this method to get all connected user IDs
            foreach (var recipientId in allConnectedUsers)
            {
                if (recipientId != userIdLong) // Don't send to the user who just disconnected
                {
                    try
                    {
                        await Clients.Group(recipientId.ToString()).SendAsync("ReceiveDeletedAccount", userIdLong);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending user status to {recipientId}: Storing in thier db...");

                        // Serialize the new delete message object
                        string serializedDeleteMessage = JsonConvert.SerializeObject(deletedAccountObject);

                        // Retrieve the recipient's existing message list from the database
                        var recipientData = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Id == recipientId && n.Deleted == false)
                            .Single();

                        List<string> messageList;
                        
                        // If the recipient's message payload is null, create a new list
                        if (recipientData.MissedPayload == null)
                        {
                            messageList = new List<string>();
                        }
                        else
                        {
                            // Deserialize the existing message list
                            messageList = JsonConvert.DeserializeObject<List<string>>(recipientData.MissedPayload);
                        }

                        // Add the new serialized delete message to the list
                        messageList.Add(serializedDeleteMessage);

                        // Serialize the updated list
                        string updatedMessageList = JsonConvert.SerializeObject(messageList);

                        // Store the updated message list back into the recipient's database
                        recipientData.MissedPayload = updatedMessageList;
                        await recipientData.Update<Userdto>();

                        Console.WriteLine("seenUnseen message stored for disconnected user.");
                    }
                }
            }

       
    }

}