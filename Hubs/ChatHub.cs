using Microsoft.AspNetCore.SignalR;
using Chat.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace Chat.Hubs
{
    public class ChatHub : Hub
    {
        private readonly AppDbContext _db;

        // відстеження онлайн-користувачів (ім'я користувача -> набір connectionId). конекшни можуть бути множинними для одного користувача, тому що зайти можна з різних вкладок браузера, з мобільного пристрою, а логін може бути один і той самий
        private static readonly ConcurrentDictionary<string, HashSet<string>> OnlineUsers = new();

        public ChatHub(AppDbContext db)
        {
            _db = db;
        }

        public async Task Connect(string userName)
        {
            userName = userName.Trim();
            if (string.IsNullOrEmpty(userName)) userName = "Анонім";

            var connectionId = Context.ConnectionId;

            var userSet = OnlineUsers.GetOrAdd(userName, _ => new HashSet<string>());
            lock (userSet)
            {
                userSet.Add(connectionId);
            }

            await Clients.All.SendAsync("UserConnected", userName);
            await UpdateOnlineUsersList();

            // відправка історії лише новому користувачу
            var history = await _db.ChatMessages
             .OrderBy(m => m.Timestamp)
             .Take(50) // обмеження до останніх 50 повідомлень
             .Select(m => new
             {
                 User = m.User ?? "Анонім",
                 Message = m.Message ?? "",
                 Timestamp = m.Timestamp.Year < 2000
                     ? DateTime.UtcNow.ToString("HH:mm:ss")
                     : m.Timestamp.ToString("HH:mm:ss")
             })
             .ToListAsync();

            await Clients.Caller.SendAsync("LoadHistory", history); // надсилаємо історію лише підключеному користувачу
        }

        public async Task SendMessage(string user, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var chatMsg = new ChatMessage
            {
                User = user,
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            _db.ChatMessages.Add(chatMsg);
            await _db.SaveChangesAsync();

            var timestamp = chatMsg.Timestamp.ToString("HH:mm:ss");

            // розсилаємо всім (включаючи відправника) з таймстампом
            await Clients.All.SendAsync("ReceiveMessage", user, message, timestamp);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;

            string? disconnectedUser = null;
            foreach (var pair in OnlineUsers)
            {
                lock (pair.Value)
                {
                    if (pair.Value.Remove(connectionId))
                    {
                        if (pair.Value.Count == 0)
                        {
                            OnlineUsers.TryRemove(pair.Key, out _);
                            disconnectedUser = pair.Key;
                        }
                        break;
                    }
                }
            }

            if (disconnectedUser != null)
            {
                await Clients.All.SendAsync("UserDisconnected", disconnectedUser);
                await UpdateOnlineUsersList();
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task UpdateOnlineUsersList()
        {
            var users = OnlineUsers.Keys.OrderBy(u => u).ToList();
            await Clients.All.SendAsync("UpdateOnlineUsers", users);
        }
    }
}