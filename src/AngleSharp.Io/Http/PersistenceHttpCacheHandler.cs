namespace AngleSharp.Io.Http
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a persistent HTTP cache handler writing one directory per
    /// origin and one file per key.
    /// </summary>
    public class PersistenceHttpCacheHandler : IHttpCacheHandler
    {
        private const Int32 FormatVersion = 1;
        private const String OriginFileName = "origin.txt";
        private const String EntryExtension = ".entry";

        private readonly String _directoryPath;

        /// <summary>
        /// Creates a new persistent HTTP cache handler for the given directory.
        /// </summary>
        /// <param name="directoryPath">The directory used to store the entries.</param>
        public PersistenceHttpCacheHandler(String directoryPath)
        {
            _directoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            Directory.CreateDirectory(_directoryPath);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<HttpCacheEntry>> ReadAsync(String origin, String key, CancellationToken cancel)
        {
            var filePath = GetEntryPath(origin, key);

            try
            {
                if (File.Exists(filePath))
                {
                    var content = await ReadAllBytesAsync(filePath, cancel).ConfigureAwait(false);
                    return Deserialize(content, key);
                }
            }
            catch (IOException)
            {
                // a concurrent write or a damaged (truncated) file is a cache miss
            }
            catch (ArgumentException)
            {
                // a damaged file may also yield negative lengths
            }

            return Array.Empty<HttpCacheEntry>();
        }

        /// <inheritdoc />
        public async Task WriteAsync(String origin, String key, IEnumerable<HttpCacheEntry> entries, CancellationToken cancel)
        {
            var originPath = GetOriginPath(origin);
            var filePath = GetEntryPath(origin, key);
            var variants = entries?.ToArray() ?? Array.Empty<HttpCacheEntry>();

            if (variants.Length == 0)
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                if (Directory.Exists(originPath) && !Directory.EnumerateFiles(originPath, "*" + EntryExtension).Any())
                {
                    Directory.Delete(originPath, true);
                }

                return;
            }

            Directory.CreateDirectory(originPath);
            File.WriteAllText(Path.Combine(originPath, OriginFileName), origin ?? String.Empty);

            // write next to the target and swap afterwards, such that readers
            // never observe a half-written entry
            var content = Serialize(key, variants);
            var tempPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                await stream.WriteAsync(content, 0, content.Length, cancel).ConfigureAwait(false);
            }

            if (File.Exists(filePath))
            {
                File.Replace(tempPath, filePath, null);
            }
            else
            {
                File.Move(tempPath, filePath);
            }
        }

        /// <inheritdoc />
        public IEnumerable<String> GetOrigins()
        {
            var result = new List<String>();

            foreach (var directory in Directory.EnumerateDirectories(_directoryPath))
            {
                var originFile = Path.Combine(directory, OriginFileName);

                if (File.Exists(originFile))
                {
                    result.Add(File.ReadAllText(originFile));
                }
            }

            return result;
        }

        /// <inheritdoc />
        public IEnumerable<String> GetKeys(String origin)
        {
            var originPath = GetOriginPath(origin);
            var result = new List<String>();

            if (Directory.Exists(originPath))
            {
                foreach (var file in Directory.EnumerateFiles(originPath, "*" + EntryExtension))
                {
                    try
                    {
                        using (var reader = new BinaryReader(File.OpenRead(file), Encoding.UTF8))
                        {
                            if (reader.ReadInt32() == FormatVersion)
                            {
                                result.Add(reader.ReadString());
                            }
                        }
                    }
                    catch (IOException)
                    {
                    }
                }
            }

            return result;
        }

        private String GetOriginPath(String origin) =>
            Path.Combine(_directoryPath, Hash(origin ?? String.Empty));

        private String GetEntryPath(String origin, String key) =>
            Path.Combine(GetOriginPath(origin), Hash(key ?? String.Empty) + EntryExtension);

        private static async Task<Byte[]> ReadAllBytesAsync(String filePath, CancellationToken cancel)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            using (var memory = new MemoryStream())
            {
                await stream.CopyToAsync(memory, 81920, cancel).ConfigureAwait(false);
                return memory.ToArray();
            }
        }

        private static Byte[] Serialize(String key, HttpCacheEntry[] variants)
        {
            using (var memory = new MemoryStream())
            {
                using (var writer = new BinaryWriter(memory, Encoding.UTF8))
                {
                    writer.Write(FormatVersion);
                    writer.Write(key ?? String.Empty);
                    writer.Write(variants.Length);

                    foreach (var entry in variants)
                    {
                        writer.Write(entry.Address ?? String.Empty);
                        writer.Write((Int32)entry.StatusCode);
                        writer.Write(entry.RequestTime.UtcTicks);
                        writer.Write(entry.ResponseTime.UtcTicks);
                        WriteHeaders(writer, entry.RequestHeaders);
                        WriteHeaders(writer, entry.ResponseHeaders);
                        var body = entry.Body ?? Array.Empty<Byte>();
                        writer.Write(body.Length);
                        writer.Write(body);
                    }
                }

                return memory.ToArray();
            }
        }

        private static IReadOnlyList<HttpCacheEntry> Deserialize(Byte[] content, String key)
        {
            using (var reader = new BinaryReader(new MemoryStream(content), Encoding.UTF8))
            {
                // a file of another format version or a hash collision is a miss
                if (reader.ReadInt32() != FormatVersion || reader.ReadString() != key)
                {
                    return Array.Empty<HttpCacheEntry>();
                }

                var count = reader.ReadInt32();
                var result = new List<HttpCacheEntry>(count);

                for (var i = 0; i < count; i++)
                {
                    var entry = new HttpCacheEntry
                    {
                        Address = reader.ReadString(),
                        StatusCode = (HttpStatusCode)reader.ReadInt32(),
                        RequestTime = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
                        ResponseTime = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
                    };
                    ReadHeaders(reader, entry.RequestHeaders);
                    ReadHeaders(reader, entry.ResponseHeaders);
                    entry.Body = reader.ReadBytes(reader.ReadInt32());
                    result.Add(entry);
                }

                return result;
            }
        }

        private static void WriteHeaders(BinaryWriter writer, IDictionary<String, String> headers)
        {
            writer.Write(headers.Count);

            foreach (var header in headers)
            {
                writer.Write(header.Key);
                writer.Write(header.Value ?? String.Empty);
            }
        }

        private static void ReadHeaders(BinaryReader reader, IDictionary<String, String> headers)
        {
            var count = reader.ReadInt32();

            for (var i = 0; i < count; i++)
            {
                var name = reader.ReadString();
                headers[name] = reader.ReadString();
            }
        }

        private static String Hash(String value)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var sb = new StringBuilder(hash.Length * 2);

                for (var i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }
    }
}
