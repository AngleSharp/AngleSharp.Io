namespace AngleSharp.Io.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Represents a persistent storage handler writing files per origin.
    /// </summary>
    public class PersistenceStorageHandler : IStorageHandler
    {
        private readonly String _directoryPath;

        /// <summary>
        /// Creates a new persistent storage handler for the given directory.
        /// </summary>
        /// <param name="directoryPath">The directory used to store origin-specific files.</param>
        public PersistenceStorageHandler(String directoryPath)
        {
            _directoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
            Directory.CreateDirectory(_directoryPath);
        }

        /// <inheritdoc />
        public IEnumerable<KeyValuePair<String, String>> Read(String origin)
        {
            var filePath = GetFilePath(origin);

            if (!File.Exists(filePath))
            {
                return Array.Empty<KeyValuePair<String, String>>();
            }

            var result = new List<KeyValuePair<String, String>>();
            var lines = File.ReadAllLines(filePath);

            foreach (var line in lines)
            {
                var index = line.IndexOf('\t');

                if (index <= 0)
                {
                    continue;
                }

                var key = Decode(line.Substring(0, index));
                var value = Decode(line.Substring(index + 1));
                result.Add(new KeyValuePair<String, String>(key, value));
            }

            return result;
        }

        /// <inheritdoc />
        public void Write(String origin, IEnumerable<KeyValuePair<String, String>> entries)
        {
            var filePath = GetFilePath(origin);
            var values = entries?.ToArray() ?? Array.Empty<KeyValuePair<String, String>>();

            if (values.Length == 0)
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                return;
            }

            var lines = values.Select(m => $"{Encode(m.Key)}\t{Encode(m.Value)}").ToArray();
            File.WriteAllLines(filePath, lines);
        }

        private String GetFilePath(String origin)
        {
            origin = origin ?? String.Empty;
            var bytes = Encoding.UTF8.GetBytes(origin);
            var hash = ComputeHash(bytes);
            var hex = ToHex(hash);
            return Path.Combine(_directoryPath, $"{hex}.storage");
        }

        private static Byte[] ComputeHash(Byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(bytes);
            }
        }

        private static String ToHex(Byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);

            for (var i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x2"));
            }

            return sb.ToString();
        }

        private static String Encode(String value) => Uri.EscapeDataString(value ?? String.Empty);

        private static String Decode(String value) => Uri.UnescapeDataString(value ?? String.Empty);
    }
}
