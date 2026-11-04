namespace AngleSharp.Io.Dom;

using AngleSharp.Dom;
using System;

static class FileUrlPolicy
{
    // like browsers, only a file: document may read file: URLs from script; anything else
    // would let a remote page read the local file system (also via a redirect to file:)
    public static Boolean IsAllowed(Url document, Url target) =>
        target == null || !IsFile(target) || (document != null && IsFile(document));

    private static Boolean IsFile(Url url) =>
        String.Equals(url.Scheme, ProtocolNames.File, StringComparison.OrdinalIgnoreCase);
}
