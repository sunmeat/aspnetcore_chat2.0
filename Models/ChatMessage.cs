namespace Chat.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public string User { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        // дата і час повідомлення у форматі UTC, зі значенням за замовчуванням
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}