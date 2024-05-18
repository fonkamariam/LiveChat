using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;


[Authorize]
public class MessagesHub : Hub
{
	private static int _connectedClients = 0;

	public override async Task OnConnectedAsync()
	{
		_connectedClients++;
		await base.OnConnectedAsync();
	}

	public override async Task OnDisconnectedAsync(Exception exception)
	{
		_connectedClients--;
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