using System;
using System.Collections.Generic;
using System.IO;

namespace VisionTests
{
    public sealed class CheckFailedException : Exception
    {
        public CheckFailedException(string message) : base(message) { }
    }

    /// <summary>Basit doğrulamalar.</summary>
    public static class Check
    {
        public static void True(bool condition, string message)
        {
            if (!condition) throw new CheckFailedException(message);
        }

        public static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new CheckFailedException(what + ": beklenen <" + expected + ">, gelen <" + actual + ">");
        }

        public static void Null(object value, string what)
        {
            if (value != null) throw new CheckFailedException(what + ": null bekleniyordu, gelen <" + value + ">");
        }

        public static void NotNull(object value, string what)
        {
            if (value == null) throw new CheckFailedException(what + ": null olmamalıydı");
        }

        public static void SameBytes(byte[] expected, byte[] actual, string what)
        {
            if (expected == null || actual == null) throw new CheckFailedException(what + ": veri yok");
            if (expected.Length != actual.Length)
                throw new CheckFailedException(what + ": boyut farklı (beklenen " + expected.Length + ", gelen " + actual.Length + ")");
            for (int i = 0; i < expected.Length; i++)
                if (expected[i] != actual[i]) throw new CheckFailedException(what + ": " + i + ". bayt farklı");
        }

        /// <summary>Ornekler klasöründeki örnek kart çıktısı (satırlar).</summary>
        public static string[] Sample(string name)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ornekler", name);
            return File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');
        }
    }
}
