namespace HexedBooster.Wrappers
{
    internal static class Logger
    {
        private static readonly Lock Gate = new();

        public static void Log(object obj) => Write(ConsoleColor.DarkCyan, obj);
        public static void LogDebug(object obj) => Write(ConsoleColor.DarkGray, obj);
        public static void LogImportant(object obj) => Write(ConsoleColor.DarkMagenta, obj);
        public static void LogSuccess(object obj) => Write(ConsoleColor.DarkGreen, obj);
        public static void LogError(object obj) => Write(ConsoleColor.Red, obj);
        public static void LogWarning(object obj) => Write(ConsoleColor.DarkYellow, obj);

        private static void Write(ConsoleColor color, object obj)
        {
            lock (Gate)
            {
                Console.ForegroundColor = color;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SteamBooster] {obj}");
                Console.ResetColor();
            }
        }
    }
}
