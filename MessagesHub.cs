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

        Console.WriteLine($" Logged IN Connected: {userIdLong}");


        var logoutHandle = await _supabaseClient.From<Userdto>()
         .Where(n => n.Id == userIdLong)
         .Single();
        logoutHandle.Status = "true";
        logoutHandle.LastSeen = DateTime.UtcNow;

        await logoutHandle.Update<Userdto>();


        await Groups.AddToGroupAsync(Context.ConnectionId, userId);

        // Update connection manager
        _userConnectionManager.AddOrUpdateUser(userId, new UserConnectionInfo
        {
            ConnectionId = Context.ConnectionId,
            IsActive = true
        });
        foreach (var user in _userConnectionManager.GetAllUsers())
        {
            if (user.Value.IsActive)
            {
                Console.WriteLine("OnConnected:Active user, Online sent");
                await Clients.All.SendAsync("UserStatusChanged", userIdLong, true);

            }
            else
            {
                Console.WriteLine("OnConnected,1 ");

                var getArrayModel = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Id == long.Parse(user.Key) && n.Deleted == false)
                    .Single();
                Console.WriteLine("OnConnected,2 ");

                // Handle OnlinePayload
                Dictionary<string, UserStatusDic> onlinePayload;
                if (string.IsNullOrEmpty(getArrayModel.OnlinePayload))
                {
                    Console.WriteLine("OnConnected,3 ");

                    onlinePayload = new Dictionary<string, UserStatusDic>();
                }
                else
                {
                    Console.WriteLine("OnConnected,4 ");

                    onlinePayload = JsonConvert.DeserializeObject<Dictionary<string, UserStatusDic>>(getArrayModel.OnlinePayload);
                }

                // Add or update the user's online status and last seen time
                onlinePayload[user.Key] = new UserStatusDic
                {
                    IsActive = true,
                    LastSeen = DateTime.UtcNow
                };

                // Serialize and store it back in the database
                getArrayModel.OnlinePayload = JsonConvert.SerializeObject(onlinePayload);
                await getArrayModel.Update<Userdto>();
                Console.WriteLine("OnConnected,5 ");

               

            }            
        }
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

        Console.WriteLine($" OnDisconnectedAsync (offline): {userIdLong}");

        Console.WriteLine("OnDisconnected,1 ");

        var logoutHandle = await _supabaseClient.From<Userdto>()
                 .Where(n => n.Id == userIdLong)
                 .Single();
        logoutHandle.Status = "false";
        logoutHandle.LastSeen = DateTime.UtcNow;

        await logoutHandle.Update<Userdto>();
        Console.WriteLine("OnDisconnected,2 ");

        //await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);

        // Update connection manager without removing the user
        if (_userConnectionManager.TryGetValue(userId, out var userInfo))
        {
            userInfo.IsActive = false;
            _userConnectionManager.AddOrUpdateUser(userId, userInfo);
            Console.WriteLine($"User {userIdLong} went offline");
            foreach (var user in _userConnectionManager.GetAllUsers())
            {
                if (user.Value.IsActive)
                {
                    Console.WriteLine("OnDisconnected: Active");
                    await Clients.All.SendAsync("UserStatusChanged", userIdLong, false);

                }
                else
                {
                    Console.WriteLine("OnDisconnected,3 ");
                    long userKeyLong = long.Parse(user.Key);
                    Console.WriteLine($"user.Key {user.Key}");
                    Console.WriteLine($"long.Parse user.key {long.Parse(user.Key)}");
                    Console.WriteLine($"vUserKEy: {userKeyLong}");

                    var getArrayModel = await _supabaseClient.From<Userdto>()
                        .Where(n => n.Id == userKeyLong && n.Deleted == false)
                        .Single();

                    // Handle OnlinePayload
                    Dictionary<string, UserStatusDic> onlinePayload;
                    if (string.IsNullOrEmpty(getArrayModel.OnlinePayload))
                    {
                        Console.WriteLine("OnDisconnected,4 ");

                        onlinePayload = new Dictionary<string, UserStatusDic>();
                    }
                    else
                    {
                        Console.WriteLine("OnDisconnected,5");

                        onlinePayload = JsonConvert.DeserializeObject<Dictionary<string, UserStatusDic>>(getArrayModel.OnlinePayload);
                    }

                    // Add or update the user's online status and last seen time
                    onlinePayload[user.Key] = new UserStatusDic
                    {
                        IsActive = false,
                        LastSeen = DateTime.UtcNow
                    };

                    // Serialize and store it back in the database
                    getArrayModel.OnlinePayload = JsonConvert.SerializeObject(onlinePayload);
                    await getArrayModel.Update<Userdto>();
                    Console.WriteLine("OnDisconnected,6");

                    Console.WriteLine("Updated OnlinePayload");

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
            if (_userConnectionManager.TryGetValue(userId, out var userInfo))
            {
                userInfo.IsActive = false;
                _userConnectionManager.AddOrUpdateUser(userId, userInfo);
                Console.WriteLine($"User {userIdLong} visibility changed to hidden");
                var logoutHandle = await _supabaseClient.From<Userdto>()
                 .Where(n => n.Id == userIdLong)
                 .Single();
                logoutHandle.Status = "false";
                logoutHandle.LastSeen = DateTime.UtcNow;

                await logoutHandle.Update<Userdto>();
                foreach (var user in _userConnectionManager.GetAllUsers())
                {
                    if (user.Value.IsActive)
                    {
                        Console.WriteLine("VisiblityChange: Active");
                        await Clients.All.SendAsync("UserStatusChanged", userIdLong, false);

                    }
                    else
                    {
                        Console.WriteLine("VisbilityChange Hidden,1 ");
                        long vUserKey = long.Parse(user.Key);
                        Console.WriteLine($"user.Key {user.Key}");
                        Console.WriteLine($"long.Parse user.key {long.Parse(user.Key)}");
                        Console.WriteLine($"vUserKEy: {vUserKey}");


                        var getArrayModel = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Id == vUserKey && n.Deleted == false)
                            .Single();

                        // Handle OnlinePayload
                        Dictionary<string, UserStatusDic> onlinePayload;
                        if (string.IsNullOrEmpty(getArrayModel.OnlinePayload))
                        {
                            Console.WriteLine("VisbilityChange Hidden,2 ");

                            onlinePayload = new Dictionary<string, UserStatusDic>();
                        }
                        else
                        {
                            Console.WriteLine("VisbilityChange Hidden,3 ");

                            onlinePayload = JsonConvert.DeserializeObject<Dictionary<string, UserStatusDic>>(getArrayModel.OnlinePayload);
                        }

                        // Add or update the user's online status and last seen time
                        onlinePayload[user.Key] = new UserStatusDic
                        {
                            IsActive = false,
                            LastSeen = DateTime.UtcNow
                        };

                        // Serialize and store it back in the database
                        getArrayModel.OnlinePayload = JsonConvert.SerializeObject(onlinePayload);
                        await getArrayModel.Update<Userdto>();
                        Console.WriteLine("VisbilityChange Hidden,4 ");

                        Console.WriteLine("Updated OnlinePayload");

                    }
                }
                //await Clients.All.SendAsync("UserStatusChanged", userIdLong, false);
            }
        }
        else
        {
            Console.WriteLine(state);
            // Handle visibility change to visible
            if (_userConnectionManager.TryGetValue(userId, out var userInfo))
            {
                Console.WriteLine("VisbilityChange Visible,1 ");

                userInfo.IsActive = true;
                _userConnectionManager.AddOrUpdateUser(userId, userInfo);
                Console.WriteLine($"User {userIdLong} visibility changed to visible");
                var logoutHandle = await _supabaseClient.From<Userdto>()
                 .Where(n => n.Id == userIdLong)
                 .Single();
                logoutHandle.Status = "true";
                logoutHandle.LastSeen = DateTime.UtcNow;

                await logoutHandle.Update<Userdto>();
                Console.WriteLine("VisbilityChange Visible,2 ");

                foreach (var user in _userConnectionManager.GetAllUsers())
                {
                    if (user.Value.IsActive)
                    {
                        Console.WriteLine("VisbilityChagne Visible, 3");
                        await Clients.All.SendAsync("UserStatusChanged", userIdLong, true);

                    }
                    else
                    {
                        Console.WriteLine("VisbilityChagne Visible, 4");

                        var getArrayModel = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Id == long.Parse(user.Key) && n.Deleted == false)
                            .Single();
                        Console.WriteLine("VisbilityChagne Visible, 5");

                        // Handle OnlinePayload
                        Dictionary<string, UserStatusDic> onlinePayload;
                        if (string.IsNullOrEmpty(getArrayModel.OnlinePayload))
                        {
                            Console.WriteLine("VisbilityChagne Visible, 6");

                            onlinePayload = new Dictionary<string, UserStatusDic>();
                        }
                        else
                        {
                            Console.WriteLine("VisbilityChagne Visible, 7");

                            onlinePayload = JsonConvert.DeserializeObject<Dictionary<string, UserStatusDic>>(getArrayModel.OnlinePayload);
                        }

                        // Add or update the user's online status and last seen time
                        onlinePayload[user.Key] = new UserStatusDic
                        {
                            IsActive = true,
                            LastSeen = DateTime.UtcNow
                        };

                        // Serialize and store it back in the database
                        getArrayModel.OnlinePayload = JsonConvert.SerializeObject(onlinePayload);
                        await getArrayModel.Update<Userdto>();
                        Console.WriteLine("VisbilityChagne Visible, 8");

                        Console.WriteLine("Updated OnlinePayload");

                    }
                }
                //await Clients.All.SendAsync("UserStatusChanged", userIdLong, true);
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

        var logoutHandle = await _supabaseClient.From<Userdto>()
            .Where(n => n.Id == userIdLong)
            .Single();
        logoutHandle.Status = "false";
        logoutHandle.LastSeen = DateTime.UtcNow;

        await logoutHandle.Update<Userdto>();

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);

        _userConnectionManager.RemoveUser(userId);

        foreach (var user in _userConnectionManager.GetAllUsers())
        {
            if (user.Value.IsActive)
            {
                Console.WriteLine("UserLogginOut: active");
                await Clients.All.SendAsync("UserStatusChanged", userIdLong, false);

            }
            else
            {
                Console.WriteLine("UserLoggintOut, 1");
                
                var getArrayModel = await _supabaseClient.From<Userdto>()
                    .Where(n => n.Id == long.Parse(user.Key) && n.Deleted == false)
                    .Single();
                Console.WriteLine("UserLoggintOut, 2");

                // Handle OnlinePayload
                Dictionary<string, UserStatusDic> onlinePayload;
                if (string.IsNullOrEmpty(getArrayModel.OnlinePayload))
                {
                    Console.WriteLine("UserLoggintOut, 3");

                    onlinePayload = new Dictionary<string, UserStatusDic>();
                }
                else
                {
                    Console.WriteLine("UserLoggintOut, 4");

                    onlinePayload = JsonConvert.DeserializeObject<Dictionary<string, UserStatusDic>>(getArrayModel.OnlinePayload);
                }

                // Add or update the user's online status and last seen time
                onlinePayload[user.Key] = new UserStatusDic
                {
                    IsActive = false,
                    LastSeen = DateTime.UtcNow
                };

                // Serialize and store it back in the database
                getArrayModel.OnlinePayload = JsonConvert.SerializeObject(onlinePayload);
                await getArrayModel.Update<Userdto>();
                Console.WriteLine("UserLoggintOut, 5");

                Console.WriteLine("Updated OnlinePayload");

            }
        }

        //await Clients.All.SendAsync("UserStatusChanged", userIdLong, false);
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
            if (_userConnectionManager.TryGetValue(userId, out var userInfo))
            {
                userInfo.IsActive = true;
                _userConnectionManager.AddOrUpdateUser(userId, userInfo);
                Console.WriteLine($"User {userIdLong} is back online");
                var logoutHandle = await _supabaseClient.From<Userdto>()
                   .Where(n => n.Id == userIdLong)
                   .Single();
                logoutHandle.Status = "true";
                logoutHandle.LastSeen = DateTime.UtcNow;

                await logoutHandle.Update<Userdto>();


                foreach (var user in _userConnectionManager.GetAllUsers())
                {
                    if (user.Value.IsActive)
                    {
                        Console.WriteLine("Online: Active");
                        await Clients.All.SendAsync("UserStatusChanged", userIdLong, true);

                    }
                    else
                    {
                        Console.WriteLine("OlineOffline, 1");

                        var getArrayModel = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Id == long.Parse(user.Key) && n.Deleted == false)
                            .Single();
                        Console.WriteLine("OlineOffline, 2");

                        // Handle OnlinePayload
                        Dictionary<string, UserStatusDic> onlinePayload;
                        if (string.IsNullOrEmpty(getArrayModel.OnlinePayload))
                        {
                            Console.WriteLine("OlineOffline, 3");

                            onlinePayload = new Dictionary<string, UserStatusDic>();
                        }
                        else
                        {
                            Console.WriteLine("OlineOffline, 4");

                            onlinePayload = JsonConvert.DeserializeObject<Dictionary<string, UserStatusDic>>(getArrayModel.OnlinePayload);
                        }

                        // Add or update the user's online status and last seen time
                        onlinePayload[user.Key] = new UserStatusDic
                        {
                            IsActive = true,
                            LastSeen = DateTime.UtcNow
                        };

                        // Serialize and store it back in the database
                        getArrayModel.OnlinePayload = JsonConvert.SerializeObject(onlinePayload);
                        await getArrayModel.Update<Userdto>();
                        Console.WriteLine("OlineOffline, 5");

                        Console.WriteLine("Updated OnlinePayload");

                    }
                }
                //await Clients.All.SendAsync("UserStatusChanged", userIdLong, true);
            }
        }
        else
        {
            Console.WriteLine("Not executed Offline");
        }
    }
}