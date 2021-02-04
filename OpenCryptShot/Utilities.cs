using System;

namespace OpenCryptShot
{
    public static class Utilities
    {
        public static void Write(ConsoleColor color, string msg)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(msg);
        }
    }
}