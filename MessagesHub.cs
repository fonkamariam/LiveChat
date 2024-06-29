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
		//Console.WriteLine("One client about to connect LOGGED IN BACKEND");
        
		var userIdclaim = Context.User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdclaim == null) 
		{ 
			Console.WriteLine("User Id is Null"); 
		}
        var userId = userIdclaim.Value.Split(':')[0].Trim();
        long userIdLong = long.Parse(userId);

        Console.WriteLine($" Logged IN Connected: {userIdLong}");
        

        var logoutHandle = await _supabaseClient.From<UserProfiledto>()
         .Where(n => n.UserId == userIdLong)
         .Single();
        logoutHandle.Status = "true";
        logoutHandle.LastSeen = DateTime.UtcNow;

        await logoutHandle.Update<UserProfiledto>();


        await Groups.AddToGroupAsync(Context.ConnectionId, userId);

        // Update connection manager
        _userConnectionManager.AddOrUpdateUser(userId, new UserConnectionInfo
        {
            ConnectionId = Context.ConnectionId,
            IsActive = true
        });

        await Clients.All.SendAsync("UserStatusChanged", userIdLong, true);
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

        Console.WriteLine($" Logged OUT DisConnected: {userIdLong}");


        var logoutHandle = await _supabaseClient.From<UserProfiledto>()
                 .Where(n => n.UserId == userIdLong)
                 .Single();
        logoutHandle.Status = "false";
        logoutHandle.LastSeen = DateTime.UtcNow;

        await logoutHandle.Update<UserProfiledto>();
       
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);

        // Update connection manager
        //_userConnectionManager.RemoveUser(userId);

        if (_connectedUsers.ContainsKey(userId))
        {
            Console.WriteLine($"User {userIdLong} went offline");

            _connectedUsers[userId] = (_connectedUsers[userId].ConnectionId, false);
            //await Clients.All.SendAsync("UserStatusChanged", userIdLong, false);
        }
        // Offline 
        //_connectedUsers.TryRemove(userId, out _);
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

    public async Task VisibilityChanged(string state)
    {
        var userIdclaim = Context.User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdclaim == null) return;

        var userId = userIdclaim.Value.Split(':')[0].Trim();
        long userIdLong = long.Parse(userId);
        Console.WriteLine(state);


        if (state == "hidden")
        {
            // Handle visibility change to hidden
            if (_connectedUsers.ContainsKey(userId))
            {
                _connectedUsers[userId] = (_connectedUsers[userId].ConnectionId, false);
                Console.WriteLine($"User {userIdLong} visibility changed to hidden");

                await Clients.All.SendAsync("UserStatusChanged", userIdLong, false);
            }
        }
        else
        {
            Console.WriteLine(state);
            // Handle visibility change to visible
            if (_connectedUsers.ContainsKey(userId))
            {
                _connectedUsers[userId] = (_connectedUsers[userId].ConnectionId, true);
                Console.WriteLine($"User {userIdLong} visibility changed to visible");

                await Clients.All.SendAsync("UserStatusChanged", userIdLong, true);
            }
        }
    }

    public async Task UserLoggingOut()
    {
        var userIdclaim = Context.User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdclaim == null) return;

        var userId = userIdclaim.Value.Split(':')[0].Trim();
        long userIdLong = long.Parse(userId);

        Console.WriteLine($"User {userIdLong} logged out intentionally");

        var logoutHandle = await _supabaseClient.From<UserProfiledto>()
            .Where(n => n.UserId == userIdLong)
            .Single();
        logoutHandle.Status = "false";
        logoutHandle.LastSeen = DateTime.UtcNow;

        await logoutHandle.Update<UserProfiledto>();

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
        
        _userConnectionManager.RemoveUser(userId);

        Console.WriteLine($"User {userIdLong} Logged out");

        await Clients.All.SendAsync("UserStatusChanged", userIdLong, false);
    }

    public async Task OnlineOffline(bool isOnline)
    {
        var userIdclaim = Context.User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdclaim == null) return;

        var userId = userIdclaim.Value.Split(':')[0].Trim();
        long userIdLong = long.Parse(userId);
        Console.WriteLine($"User {userIdLong} is {isOnline}");

        if (isOnline)
        {

            // Handle reconnection
            if (_connectedUsers.ContainsKey(userId))
            {
                Console.WriteLine($"User {userIdLong} is back online");

                _connectedUsers[userId] = (_connectedUsers[userId].ConnectionId, true);
                await Clients.All.SendAsync("UserStatusChanged", userIdLong, true);
            }
        }
        else
        {
            Console.WriteLine($"User {userIdLong} is offline ");

            // Handle disconnection
            if (_connectedUsers.ContainsKey(userId))
            { 
                Console.WriteLine($"User {userIdLong} went offline");

                _connectedUsers[userId] = (_connectedUsers[userId].ConnectionId, false);
                await Clients.All.SendAsync("UserStatusChanged", userIdLong, false);
            }
        }
    }
}