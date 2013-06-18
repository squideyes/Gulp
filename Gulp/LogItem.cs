using System;

namespace Gulp
{
    public class LogItem
    {
        public LogItem(LogKind kind, string format, params object[] args)
            : this(kind, string.Format(format, args))
        {
        }

        public LogItem(LogKind kind, string message)
        {
            Kind = kind;
            AddedOn = DateTime.Now;
            Message = message;
        }

        public LogKind Kind { get; private set; }
        public DateTime AddedOn { get; private set; }
        public string Message { get; private set; }

        public ConsoleColor Color
        {
            get
            {
                switch (Kind)
                {
                    case LogKind.DupHTML:
                        return ConsoleColor.Blue;
                    case LogKind.DupMedia:
                        return ConsoleColor.Cyan;
                    case LogKind.Error:
                        return ConsoleColor.Red;
                    case LogKind.Fetch:
                        return ConsoleColor.Green;
                    case LogKind.Scrape:
                        return ConsoleColor.White;
                    case LogKind.Warning:
                        return ConsoleColor.Magenta;
                    default:
                        return ConsoleColor.White;
                }
            }
        }
    }
}
