using System.Text;

namespace SpotifyClicker.Core
{
    public static class EncodingTools
    {
        public static string Encode(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            StringBuilder result = new StringBuilder();
            const string hex = "0123456789ABCDEF";
            foreach (byte b in bytes)
                if ((b >= 65 && b <= 90) || (b >= 97 && b <= 122) || (b >= 48 && b <= 57) || b == 45 || b == 46 || b == 95 || b == 126) result.Append((char)b);
                else { result.Append('%'); result.Append(hex[b >> 4]); result.Append(hex[b & 15]); }
            return result.ToString();
        }
    }
}
