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
    private static readonly ConcurrentDictionary<string, (string ConnectionId, bool IsActive)> _connectedUsers = new ConcurrentDictionary<string, (string ConnectionId, bool IsActive)>();
    private readonly Supabase.Client _supabaseClient;
    private readonly IConfiguration _configuration;
    private readonly UserConnectionManager _userConnectionManager;
    public MessagesHub(IConfiguration configuration, Client supabaseClient, UserConnectionManager userConnectionManager)
    {
        _configuration = configuration;
        _supabaseClient = supabaseClient;
        _userConnectionManager = userConnectionManager;
        
    }
    
    public override async Task OnConnectedAsync()
    {
        
        var userIdclaim = Context.User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdclaim == null)
        {
            Console.WriteLine("User Id is Null");
        }
        var userId = userIdclaim.Value.Split(':')[0].Trim();
        long userIdLong = long.Parse(userId);
        DateTime dateTime = DateTime.UtcNow;
        
        Console.WriteLine($" Logged IN Connected: {userIdLong}");

        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        
        await Clients.All.SendAsync("UserStatusChanged", userIdLong, true, dateTime);
        var logoutHandle = await _supabaseClient.From<Userdto>()
         .Where(n => n.Id == userIdLong)
         .Single();
        logoutHandle.Status = "true";
        logoutHandle.LastSeen = dateTime;

        await logoutHandle.Update<Userdto>();


        
       
        await base.OnConnectedAsync();
    }

	public override async Task OnDisconnectedAsync(Exception exception)
	{
        
        var userIdclaim = Context.User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdclaim == null)
        {
            Console.WriteLine("Email is Null");
        }
        var userId = userIdclaim.Value.Split(':')[0].Trim();
        long userIdLong = long.Parse(userId);

        Console.WriteLine($" OnDisconnectedAsync: {userIdLong}");

        
        var logoutHandle = await _supabaseClient.From<Userdto>()
                 .Where(n => n.Id == userIdLong)
                 .Single();
        logoutHandle.Status = "false";
        DateTime dateTime = DateTime.UtcNow;
        logoutHandle.LastSeen = dateTime;
        

        await logoutHandle.Update<Userdto>();
        
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);

        await Clients.All.SendAsync("UserStatusChanged", userIdLong, false);

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
        DateTime dateTime = DateTime.UtcNow;
        
        
        if (state == "hidden")
        {
           
            // Handle visibility change to hidden
            await Clients.All.SendAsync("UserStatusChanged", userIdLong,false,dateTime);
            
        }
        else
        {
            // Handle visibility change to visible
            await Clients.All.SendAsync("UserStatusChanged", userIdLong,true,dateTime);
           
        }
    }

    public async Task UserLoggingOutTask(long userIdLong)
    {
        var logoutHandle = await _supabaseClient.From<Userdto>()
            .Where(n => n.Id == userIdLong)
            .Single();
        logoutHandle.Status = "false";
        DateTime dateTime = DateTime.UtcNow;
        logoutHandle.LastSeen = dateTime;
        

        await logoutHandle.Update<Userdto>();

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userIdLong.ToString());

        await Clients.All.SendAsync("UserStatusChanged", userIdLong, false);
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
        await Clients.Group(recipient.ToString()).SendAsync("ReceiveSendMessage", messageObject);
    }

    public async Task HandleEditMessageTask(long recipient, MessageDto messageObject)
    {
        await Clients.Group(recipient.ToString()).SendAsync("ReceiveEditMessage", messageObject);
    }

    public async Task HandleDeleteMessageTask(long recipient, long messageId,long convId)
    {
        await Clients.Group(recipient.ToString()).SendAsync("ReceiveDeleteMessage", messageId,convId);
    }
    
    public async Task HanldeSeenUnseenTask (long recpient,long messageId,long convId) 
    {
        await Clients.Group(recpient.ToString()).SendAsync("ReceiveSeenUnseen", messageId,convId);
    }
    
    public async Task HanldeUserProfileTask (long userId, UserProfileFrontend userProfile) 
    {
        await Clients.All.SendAsync("UserProfileChanged", userProfile);
    }
    
}