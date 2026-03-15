namespace MineMogul.CreativeMode.Utilities;

internal static class CreativeNameUtils
{
    internal static string SanitizeName(string source, string fallback)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return fallback;
        }

        char[] chars = source.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }
}
