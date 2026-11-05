namespace Mongle
{
    /// <summary>Korean particles that depend on the last syllable of a name (몽이가 / 치즈가, 보리를 / 부릉이를).</summary>
    public static class Josa
    {
        static bool HasFinal(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            char c = word[word.Length - 1];
            if (c < 0xAC00 || c > 0xD7A3) return false;
            return (c - 0xAC00) % 28 != 0;
        }
        public static string IGa(string w) => w + (HasFinal(w) ? "이" : "가");
        public static string EulReul(string w) => w + (HasFinal(w) ? "을" : "를");
        public static string EunNeun(string w) => w + (HasFinal(w) ? "은" : "는");
        public static string WaGwa(string w) => w + (HasFinal(w) ? "과" : "와");
    }
}
