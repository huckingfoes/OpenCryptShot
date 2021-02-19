namespace OpenCryptShot.Discord
{
    public class Message
    {
        public string id { get; set; }
        public int type { get; set; }
        public string content { get; set; }
        public string channel_id { get; set; }
        public bool mention_everyone { get; set; }
        public string timestamp { get; set; }
        public string edited_timestamp { get; set; }
        public int flags { get; set; }
    }
}