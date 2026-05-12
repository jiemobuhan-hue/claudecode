using System;

namespace ZenergyBFSI.Model
{
    public class Log
    {
        public string Time { get; set; } = DateTime.Now.ToString();
        public string Type { get; set; }
        public string Content { get; set; }
        public string Foreground { get; set; } = "black";

        public Log(string type, string foreground, string content)
        {
            Type = type;
            Foreground = foreground;
            Content = content;
        }
    }
}
