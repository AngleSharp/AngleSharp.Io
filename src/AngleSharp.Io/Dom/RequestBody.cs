namespace AngleSharp.Io.Dom
{
    using AngleSharp.Dom;
    using AngleSharp.Html;
    using System;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Serializes the body forms accepted by fetch, XMLHttpRequest, and
    /// sendBeacon.
    /// </summary>
    static class RequestBody
    {
        public static SerializedBody Serialize(Object body)
        {
            if (body == null)
            {
                return SerializedBody.Empty;
            }

            switch (body)
            {
                case Stream stream:
                    if (stream.CanSeek)
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                    }

                    return new SerializedBody(stream);
                case Byte[] bytes:
                    return new SerializedBody(new MemoryStream(bytes));
                case ArraySegment<Byte> segment when segment.Array != null:
                    return new SerializedBody(new MemoryStream(segment.Array, segment.Offset, segment.Count, false));
                case FormDataSet formDataSet:
                    var formDataBody = formDataSet.AsMultipart(null, Encoding.UTF8);
                    var formDataType = String.Concat("multipart/form-data; boundary=", formDataSet.Boundary);
                    return new SerializedBody(formDataBody, formDataType);
                case UrlSearchParams searchParams:
                    var query = searchParams.ToString();
                    var queryBytes = Encoding.UTF8.GetBytes(query);
                    return new SerializedBody(new MemoryStream(queryBytes), "application/x-www-form-urlencoded; charset=UTF-8");
                case IBlob blob:
                    var blobBody = blob.Body;

                    if (blobBody != null)
                    {
                        if (blobBody.CanSeek)
                        {
                            blobBody.Seek(0, SeekOrigin.Begin);
                        }

                        return new SerializedBody(blobBody, blob.Type);
                    }

                    break;
            }

            var content = body.ToString();
            var contentBytes = Encoding.UTF8.GetBytes(content);
            return new SerializedBody(new MemoryStream(contentBytes));
        }
    }

    readonly struct SerializedBody
    {
        public static readonly SerializedBody Empty = new SerializedBody(Stream.Null);

        public SerializedBody(Stream content, String contentType = null)
        {
            Content = content ?? Stream.Null;
            ContentType = contentType;
        }

        public Stream Content { get; }

        public String ContentType { get; }
    }
}
