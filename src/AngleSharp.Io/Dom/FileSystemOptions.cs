namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The kinds of file system handles.
    /// </summary>
    [DomLiterals]
    [DomName("FileSystemHandleKind")]
    public enum FileSystemHandleKind
    {
        /// <summary>
        /// The handle represents a file.
        /// </summary>
        [DomName("file")]
        File,
        /// <summary>
        /// The handle represents a directory.
        /// </summary>
        [DomName("directory")]
        Directory,
    }

    /// <summary>
    /// The access modes of a file system handle.
    /// </summary>
    [DomLiterals]
    [DomName("FileSystemPermissionMode")]
    public enum FileSystemPermissionMode
    {
        /// <summary>
        /// The file can be read.
        /// </summary>
        [DomName("read")]
        Read,
        /// <summary>
        /// The file can be read and written.
        /// </summary>
        [DomName("readwrite")]
        ReadWrite,
    }

    /// <summary>
    /// The commands of a <see cref="WriteParams"/>.
    /// </summary>
    [DomLiterals]
    [DomName("WriteCommandType")]
    public enum WriteCommandType
    {
        /// <summary>
        /// Writes the data.
        /// </summary>
        [DomName("write")]
        Write,
        /// <summary>
        /// Moves the position.
        /// </summary>
        [DomName("seek")]
        Seek,
        /// <summary>
        /// Resizes the file.
        /// </summary>
        [DomName("truncate")]
        Truncate,
    }

    /// <summary>
    /// Describes the access to query or request for a handle.
    /// </summary>
    [DomName("FileSystemHandlePermissionDescriptor")]
    public sealed class FileSystemHandlePermissionDescriptor
    {
        /// <summary>
        /// Gets or sets the access mode.
        /// </summary>
        [DomName("mode")]
        public FileSystemPermissionMode Mode { get; set; } = FileSystemPermissionMode.Read;
    }

    /// <summary>
    /// Represents the options for creating a writable stream.
    /// </summary>
    [DomName("FileSystemCreateWritableOptions")]
    public sealed class FileSystemCreateWritableOptions
    {
        /// <summary>
        /// Gets or sets if the stream starts with the content of the file
        /// instead of being empty.
        /// </summary>
        [DomName("keepExistingData")]
        public Boolean KeepExistingData { get; set; }
    }

    /// <summary>
    /// Represents a command for a writable stream.
    /// </summary>
    [DomName("WriteParams")]
    public sealed class WriteParams
    {
        /// <summary>
        /// Gets or sets the command.
        /// </summary>
        [DomName("type")]
        public WriteCommandType Type { get; set; }

        /// <summary>
        /// Gets or sets the new size, which is required for truncate.
        /// </summary>
        [DomName("size")]
        public Double? Size { get; set; }

        /// <summary>
        /// Gets or sets the position, which is required for seek and optional
        /// for write.
        /// </summary>
        [DomName("position")]
        public Double? Position { get; set; }

        /// <summary>
        /// Gets or sets the data, which is required for write, e.g., a string,
        /// a byte array, or a blob.
        /// </summary>
        [DomName("data")]
        public Object Data { get; set; }
    }

    /// <summary>
    /// Describes the files offered by a picker.
    /// </summary>
    [DomName("FilePickerAcceptType")]
    public sealed class FilePickerAcceptType
    {
        /// <summary>
        /// Gets or sets the description of the files.
        /// </summary>
        [DomName("description")]
        public String Description { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the accepted file suffixes (e.g., ".txt") per MIME type.
        /// </summary>
        [DomName("accept")]
        public Dictionary<String, FilePickerSuffixes> Accept { get; set; }
    }

    /// <summary>
    /// Represents the file suffixes accepted for a MIME type, which are given as
    /// either a single suffix or several of them.
    /// </summary>
    public sealed class FilePickerSuffixes
    {
        /// <summary>
        /// Creates new suffixes.
        /// </summary>
        /// <param name="values">The suffixes, e.g., ".txt".</param>
        public FilePickerSuffixes(params String[] values)
        {
            Values = values ?? Array.Empty<String>();
        }

        /// <summary>
        /// Gets the suffixes.
        /// </summary>
        public String[] Values { get; }

        /// <summary>
        /// Converts a single suffix.
        /// </summary>
        /// <param name="value">The suffix.</param>
        public static implicit operator FilePickerSuffixes(String value) => new FilePickerSuffixes(new[] { value });

        /// <summary>
        /// Converts several suffixes.
        /// </summary>
        /// <param name="values">The suffixes.</param>
        public static implicit operator FilePickerSuffixes(String[] values) => new FilePickerSuffixes(values);
    }

    /// <summary>
    /// Represents the options common to all pickers.
    /// </summary>
    [DomName("FilePickerOptions")]
    public abstract class FilePickerOptions
    {
        private const Int32 MaxIdLength = 32;
        private const Int32 MaxSuffixLength = 16;

        /// <summary>
        /// Gets or sets the files offered by the picker.
        /// </summary>
        [DomName("types")]
        public FilePickerAcceptType[] Types { get; set; }

        /// <summary>
        /// Gets or sets if the picker does not offer all files in addition.
        /// </summary>
        [DomName("excludeAcceptAllOption")]
        public Boolean ExcludeAcceptAllOption { get; set; }

        /// <summary>
        /// Gets or sets the identifier for remembering the directory of the picker.
        /// </summary>
        [DomName("id")]
        public String Id { get; set; }

        /// <summary>
        /// Gets or sets the well-known directory to start in, e.g., "documents".
        /// </summary>
        [DomName("startIn")]
        public String StartIn { get; set; }

        // the spec rejects all of these with a TypeError
        internal void Validate()
        {
            if (Id != null && (Id.Length > MaxIdLength || !Id.All(IsIdCharacter)))
            {
                throw new ArgumentException($"The id '{Id}' is not valid.");
            }

            foreach (var type in Types ?? Array.Empty<FilePickerAcceptType>())
            {
                if (type?.Accept is null || type.Accept.Count == 0)
                {
                    throw new ArgumentException("An accept type has to list its files.");
                }

                foreach (var entry in type.Accept)
                {
                    if (!IsMimeType(entry.Key))
                    {
                        throw new ArgumentException($"The MIME type '{entry.Key}' is not valid.");
                    }

                    foreach (var suffix in entry.Value?.Values ?? Array.Empty<String>())
                    {
                        if (!IsSuffix(suffix))
                        {
                            throw new ArgumentException($"The file suffix '{suffix}' is not valid.");
                        }
                    }
                }
            }

            if (ExcludeAcceptAllOption && (Types is null || Types.Length == 0))
            {
                throw new ArgumentException("A picker excluding all files has to accept some files.");
            }
        }

        private static Boolean IsIdCharacter(Char c) =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';

        private static Boolean IsMimeType(String value)
        {
            var separator = value.IndexOf('/');

            return separator > 0 &&
                separator < value.Length - 1 &&
                value.IndexOf('/', separator + 1) < 0 &&
                value.IndexOf(';') < 0 &&
                !value.Any(Char.IsWhiteSpace);
        }

        private static Boolean IsSuffix(String value) =>
            value != null &&
            value.Length > 1 &&
            value.Length <= MaxSuffixLength &&
            value[0] == '.' &&
            value[value.Length - 1] != '.' &&
            value.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '+' || c == '.');
    }

    /// <summary>
    /// Represents the options of window.showOpenFilePicker().
    /// </summary>
    [DomName("OpenFilePickerOptions")]
    public sealed class OpenFilePickerOptions : FilePickerOptions
    {
        /// <summary>
        /// Gets or sets if multiple files can be picked.
        /// </summary>
        [DomName("multiple")]
        public Boolean Multiple { get; set; }
    }

    /// <summary>
    /// Represents the options of window.showSaveFilePicker().
    /// </summary>
    [DomName("SaveFilePickerOptions")]
    public sealed class SaveFilePickerOptions : FilePickerOptions
    {
        /// <summary>
        /// Gets or sets the suggested name of the file.
        /// </summary>
        [DomName("suggestedName")]
        public String SuggestedName { get; set; }
    }
}
