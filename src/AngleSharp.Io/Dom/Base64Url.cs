namespace AngleSharp.Io.Dom;

using System;

static class Base64Url
{
    public static String Encode(Byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static Boolean TryDecode(String value, out Byte[] result)
    {
        var padding = (4 - (value.Length % 4)) % 4;
        var base64 = value.Replace('-', '+').Replace('_', '/') + new String('=', padding);

        // the plain alphabet is not part of base64url
        if (value.IndexOf('+') < 0 && value.IndexOf('/') < 0)
        {
            try
            {
                result = Convert.FromBase64String(base64);
                return true;
            }
            catch (FormatException)
            {
            }
        }

        result = null;
        return false;
    }
}
