namespace AngleSharp.Io;

using AngleSharp.Text;
using System;
using System.Text;

/// <summary>
/// Gives access to AngleSharp's <see cref="TextEncoding"/>, making sure the
/// legacy encodings are available first.
/// </summary>
internal static class EncodingSupport
{
    static EncodingSupport()
    {
        //  TextEncoding looks its encodings up once, in its static fields. AngleSharp only
        //  registers the code pages when Configuration is first used, so touching
        //  TextEncoding before that (e.g., a TextDecoderStream without any browsing
        //  context) would leave every legacy encoding as UTF-8 for the whole process
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Encoding Utf8 => TextEncoding.Utf8;

    public static Boolean IsSupported(String label) => TextEncoding.IsSupported(label);

    public static Encoding Resolve(String label) => TextEncoding.Resolve(label);
}
