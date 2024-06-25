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
[Authorize]
public class MessagesHub : Hub
{
	private static int _connectedClients = 0;
    private static readonly ConcurrentDictionary<string, string> _connectedUsers = new ConcurrentDictionary<string, string>();
    private readonly Supabase.Client _supabaseClient;
    private readonly IConfiguration _configuration;
    public MessagesHub(IConfiguration configuration, Client supabaseClient)
    {
        _configuration = configuration;
        _supabaseClient = supabaseClient;
    }

    public override async Task OnConnectedAsync()
	{
		//Console.WriteLine("One client about to connect LOGGED IN BACKEND");
        
		var userIdclaim = Context.User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdclaim == null) 
		{ 
			Console.WriteLine("User Id is Null"); 
		}
        var userId = userIdclaim.Value.Split(':')[0].Trim();
        long userIdLong = long.Parse(userId);

        //Console.WriteLine($"About to Login: {userIdLong}");
        

        var logoutHandle = await _supabaseClient.From<UserProfiledto>()
         .Where(n => n.UserId == userIdLong)
         .Single();
        logoutHandle.Status = "true";
        logoutHandle.LastSeen = DateTime.UtcNow;

        await logoutHandle.Update<UserProfiledto>();
        
        //Console.WriteLine($"{userIdLong} Logged in");


        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        _connectedClients++;
        // Online
        _connectedUsers[userId] = Context.ConnectionId;
        await Clients.All.SendAsync("UserStatusChanged", userIdLong, true);
        // Online
        await base.OnConnectedAsync();
	}

	public override async Task OnDisconnectedAsync(Exception exception)
	{
        //Console.WriteLine("One client about to Disconnect LOGOUT BACKEND");

        var userIdclaim = Context.User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdclaim == null)
        {
            Console.WriteLine("Email is Null");
        }
        var userId = userIdclaim.Value.Split(':')[0].Trim();
        long userIdLong = long.Parse(userId);

        //Console.WriteLine($"About to Login: {userIdLong}");

        var logoutHandle = await _supabaseClient.From<UserProfiledto>()
                 .Where(n => n.UserId == userIdLong)
                 .Single();
        logoutHandle.Status = "false";
        logoutHandle.LastSeen = DateTime.UtcNow;

        await logoutHandle.Update<UserProfiledto>();
       
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);

        
        _connectedClients--;
        
        // Offline
        _connectedUsers.TryRemove(userId, out _);
        await Clients.All.SendAsync("UserStatusChanged", userIdLong, false);
        //Console.WriteLine($"{userIdLong} LoggedOut");

        // Offline

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
}