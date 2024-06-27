using System.Collections.Concurrent;

    public class UserConnectionManager
    {
        private readonly ConcurrentDictionary<string, UserConnectionInfo> _connectedUsers = new ConcurrentDictionary<string, UserConnectionInfo>();

        public bool TryGetValue(string userId, out UserConnectionInfo userInfo)
        {
            return _connectedUsers.TryGetValue(userId, out userInfo);
        }

        public void AddOrUpdateUser(string userId, UserConnectionInfo userInfo)
        {
            _connectedUsers[userId] = userInfo;
        }

        public void RemoveUser(string userId)
        {
            _connectedUsers.TryRemove(userId, out _);
        }

        public ConcurrentDictionary<string, UserConnectionInfo> GetAllUsers()
        {
            return _connectedUsers;
        }

        // Add other necessary methods to manage user connections
    }

    public class UserConnectionInfo
    {
        public string ConnectionId { get; set; }
        public bool IsActive { get; set; }
    }

