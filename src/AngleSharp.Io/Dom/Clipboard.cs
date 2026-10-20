namespace AngleSharp.Io.Dom
{
    using AngleSharp.Attributes;
    using AngleSharp.Io.PermissionsApi;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the Clipboard API.
    /// </summary>
    [DomName("Clipboard")]
    public sealed class Clipboard
    {
        private readonly IClipboardPlatform _platform;
        private readonly Permissions _permissions;

        /// <summary>
        /// Creates a new clipboard wrapper using the provided platform implementation.
        /// </summary>
        /// <param name="platform">The platform clipboard implementation.</param>
        public Clipboard(IClipboardPlatform platform)
            : this(platform, null)
        {
        }

        /// <summary>
        /// Creates a new clipboard wrapper using the provided platform
        /// implementation, which is only used when the permissions allow it.
        /// </summary>
        /// <param name="platform">The platform clipboard implementation.</param>
        /// <param name="permissions">The permissions to check, or null to allow every access.</param>
        public Clipboard(IClipboardPlatform platform, Permissions permissions)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _permissions = permissions;
        }

        /// <summary>
        /// Reads text from the clipboard.
        /// </summary>
        /// <returns>The clipboard text.</returns>
        [DomName("readText")]
        public async Task<String> ReadText()
        {
            if (_permissions != null)
            {
                await _permissions.EnsureGrantedAsync(PermissionNames.ClipboardRead).ConfigureAwait(false);
            }

            return await _platform.ReadTextAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Writes text to the clipboard.
        /// </summary>
        /// <param name="text">The text to write.</param>
        /// <returns>The task representing the asynchronous operation.</returns>
        [DomName("writeText")]
        public async Task WriteText(String text)
        {
            if (_permissions != null)
            {
                await _permissions.EnsureGrantedAsync(PermissionNames.ClipboardWrite).ConfigureAwait(false);
            }

            await _platform.WriteTextAsync(text ?? String.Empty).ConfigureAwait(false);
        }
    }
}