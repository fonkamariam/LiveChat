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
        DateTime dateTime = DateTime.UtcNow;
        logoutHandle.LastSeen = dateTime;

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
            if (user.Key != userIdLong.ToString())
            {
                if (user.Value.IsActive)
                {
                    await Clients.All.SendAsync("UserStatusChanged", userIdLong, true,dateTime);
                }
                else
                {
                    
                    long onConnectedLong = long.Parse(user.Key);
                    var getArrayModel = await _supabaseClient.From<Userdto>()
                        .Where(n => n.Id == onConnectedLong && n.Deleted == false)
                        .Single();
                    
                    // Handle OnlinePayload
                    Dictionary<string, UserStatusDic> onlinePayload;
                    if (string.IsNullOrEmpty(getArrayModel.OnlinePayload))
                    {
                        
                        onlinePayload = new Dictionary<string, UserStatusDic>();
                    }
                    else
                    {
                        
                        onlinePayload = JsonConvert.DeserializeObject<Dictionary<string, UserStatusDic>>(getArrayModel.OnlinePayload);
                    }

                    // Add or update the user's online status and last seen time
                    onlinePayload[userIdLong.ToString()] = new UserStatusDic
                    {
                        IsActive = true,
                        LastSeen = dateTime
                    };

                    // Serialize and store it back in the database
                    getArrayModel.OnlinePayload = JsonConvert.SerializeObject(onlinePayload);

                    await getArrayModel.Update<Userdto>();
                    
                }
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

        Console.WriteLine($" OnDisconnectedAsync: {userIdLong}");

        
        var logoutHandle = await _supabaseClient.From<Userdto>()
                 .Where(n => n.Id == userIdLong)
                 .Single();
        logoutHandle.Status = "false";
        DateTime dateTime = DateTime.UtcNow;
        logoutHandle.LastSeen = dateTime;
        logoutHandle.OnlinePayload = null;
        logoutHandle.ConvPayload = null;
        logoutHandle.UserPayload = null;
        logoutHandle.OnlinePayload = null;

        await logoutHandle.Update<Userdto>();
        Console.WriteLine("Updated Disconnection");

        //await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);

        // Update connection manager without removing the user
        if (_userConnectionManager.TryGetValue(userId, out var userInfo))
        {
            userInfo.IsActive = false;
            _userConnectionManager.AddOrUpdateUser(userId, userInfo);
            foreach (var user in _userConnectionManager.GetAllUsers())
            {

                if (user.Key != userIdLong.ToString())
                {
                    if (user.Value.IsActive)
                    {
                        Console.WriteLine("OnDisconnected: Active");
                        await Clients.All.SendAsync("UserStatusChanged", userIdLong, false,dateTime);

                    }
                    else
                    {
                        long userKeyLong = long.Parse(user.Key);

                        var getArrayModel = await _supabaseClient.From<Userdto>()
                            .Where(n => n.Id == userKeyLong && n.Deleted == false)
                            .Single();

                        // Handle OnlinePayload
                        Dictionary<string, UserStatusDic> onlinePayload;
                        if (string.IsNullOrEmpty(getArrayModel.OnlinePayload))
                        {
                        
                            onlinePayload = new Dictionary<string, UserStatusDic>();
                        }
                        else
                        {
                           
                            onlinePayload = JsonConvert.DeserializeObject<Dictionary<string, UserStatusDic>>(getArrayModel.OnlinePayload);
                        }

                        // Add or update the user's online status and last seen time
                        onlinePayload[userIdLong.ToString()] = new UserStatusDic
                        {
                            IsActive = false,
                            LastSeen = dateTime
                        };

                        // Serialize and store it back in the database
                        getArrayModel.OnlinePayload = JsonConvert.SerializeObject(onlinePayload);
                        await getArrayModel.Update<Userdto>();
                       
                        
                    }
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
        
        if (state == "hidden")
        {
            Console.WriteLine(state);

            // Handle visibility change to hidden
            if (_userConnectionManager.TryGetValue(userId, out var userInfo))
            {
                userInfo.IsActive = false;
                _userConnectionManager.AddOrUpdateUser(userId, userInfo);
                var logoutHandle = await _supabaseClient.From<Userdto>()
                 .Where(n => n.Id == userIdLong)
                 .Single();
                logoutHandle.Status = "false";
                DateTime dateTime = DateTime.UtcNow;
                logoutHandle.LastSeen = dateTime;

                await logoutHandle.Update<Userdto>();
                foreach (var user in _userConnectionManager.GetAllUsers())
                {
                    if (user.Key != userIdLong.ToString())
                    {
                        if (user.Value.IsActive)
                        {
                            await Clients.All.SendAsync("UserStatusChanged", userIdLong, false, dateTime);

                        }
                        else
                        {
                            long vUserKey = long.Parse(user.Key);
                            

                            var getArrayModel = await _supabaseClient.From<Userdto>()
                                .Where(n => n.Id == vUserKey && n.Deleted == false)
                                .Single();

                            // Handle OnlinePayload
                            Dictionary<string, UserStatusDic> onlinePayload;
                            if (string.IsNullOrEmpty(getArrayModel.OnlinePayload))
                            {
                                
                                onlinePayload = new Dictionary<string, UserStatusDic>();
                            }
                            else
                            {
                                
                                onlinePayload = JsonConvert.DeserializeObject<Dictionary<string, UserStatusDic>>(getArrayModel.OnlinePayload);
                            }

                            // Add or update the user's online status and last seen time
                            onlinePayload[userIdLong.ToString()] = new UserStatusDic
                            {
                                IsActive = false,
                                LastSeen = dateTime
                            };

                            // Serialize and store it back in the database
                            getArrayModel.OnlinePayload = JsonConvert.SerializeObject(onlinePayload);
                            await getArrayModel.Update<Userdto>();
                            
                        }
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
                 .Where(n => n.Id == userIdLong&& n.Deleted ==false)
                 .Single();
                logoutHandle.Status = "true";
                DateTime dateTime = DateTime.UtcNow;
                logoutHandle.LastSeen = dateTime;


                await logoutHandle.Update<Userdto>();
                
                foreach (var user in _userConnectionManager.GetAllUsers())
                {
                    if (user.Key != userIdLong.ToString())
                    {
                        if (user.Value.IsActive)
                        {
                            await Clients.All.SendAsync("UserStatusChanged", userIdLong, true, dateTime);

                        }
                        else
                        {
                            long visibleLong = long.Parse(user.Key);
                            var getArrayModel = await _supabaseClient.From<Userdto>()
                                .Where(n => n.Id == visibleLong && n.Deleted == false)
                                .Single();
                            
                            // Handle OnlinePayload
                            Dictionary<string, UserStatusDic> onlinePayload;
                            if (string.IsNullOrEmpty(getArrayModel.OnlinePayload))
                            {
                                
                                onlinePayload = new Dictionary<string, UserStatusDic>();
                            }
                            else
                            {
                                
                                onlinePayload = JsonConvert.DeserializeObject<Dictionary<string, UserStatusDic>>(getArrayModel.OnlinePayload);
                            }

                            // Add or update the user's online status and last seen time
                            onlinePayload[userIdLong.ToString()] = new UserStatusDic
                            {
                                IsActive = true,
                                LastSeen = dateTime
                            };

                            // Serialize and store it back in the database
                            getArrayModel.OnlinePayload = JsonConvert.SerializeObject(onlinePayload);
                            await getArrayModel.Update<Userdto>();
                            
                        }
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
        DateTime dateTime = DateTime.UtcNow;
        logoutHandle.LastSeen = dateTime;
        logoutHandle.OnlinePayload = null;

        await logoutHandle.Update<Userdto>();

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);

        _userConnectionManager.RemoveUser(userId);

        foreach (var user in _userConnectionManager.GetAllUsers())
        {
            if (user.Key != userIdLong.ToString())
            {
                if (user.Value.IsActive)
                {
                    Console.WriteLine("UserLogged out");

                    await Clients.All.SendAsync("UserStatusChanged", userIdLong, false,dateTime);

                }
                else
                {
                    Console.WriteLine("UserLoggintOut");
                    long logoutLong = long.Parse(user.Key);
                    var getArrayModel = await _supabaseClient.From<Userdto>()
                        .Where(n => n.Id == logoutLong && n.Deleted == false)
                        .Single();
                    
                    // Handle OnlinePayload
                    Dictionary<string, UserStatusDic> onlinePayload;
                    if (string.IsNullOrEmpty(getArrayModel.OnlinePayload))
                    {
                       
                        onlinePayload = new Dictionary<string, UserStatusDic>();
                    }
                    else
                    {
                        
                        onlinePayload = JsonConvert.DeserializeObject<Dictionary<string, UserStatusDic>>(getArrayModel.OnlinePayload);
                    }

                    // Add or update the user's online status and last seen time
                    onlinePayload[userIdLong.ToString()] = new UserStatusDic
                    {
                        IsActive = false,
                        LastSeen = dateTime
                    };

                    // Serialize and store it back in the database
                    getArrayModel.OnlinePayload = JsonConvert.SerializeObject(onlinePayload);
                    await getArrayModel.Update<Userdto>();
                    
                    
                }
            }
                
        }

        //await Clients.All.SendAsync("UserStatusChanged", userIdLong, false);
    }

    public async Task TypingIndicator(long idPara,bool valuePara)
    {
        var userIdclaim = Context.User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdclaim == null) return;

        var userId = userIdclaim.Value.Split(':')[0].Trim();
        long userIdLong = long.Parse(userId);
        
        await Clients.Group(idPara.ToString()).SendAsync("Typing", userIdLong,valuePara);

    }

}