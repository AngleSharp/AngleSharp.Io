namespace AngleSharp.Io.Tests.FileSystem
{
    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.FileSystemApi;
    using AngleSharp.Io.PermissionsApi;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [TestFixture]
    public class FileSystemAccessTests
    {
        private const String Address = "https://files.example/app";
        private const String Origin = "https://files.example";

        [Test]
        public async Task PickersWithoutPlatformAreNotSupported()
        {
            var window = await OpenWindowAsync(Configuration.Default).ConfigureAwait(false);

            AssertRejected(DomError.NotSupported, () => window.ShowOpenFilePicker());
            AssertRejected(DomError.NotSupported, () => window.ShowSaveFilePicker());
        }

        [Test]
        public async Task OpenPickerReturnsAHandleToThePickedFile()
        {
            var platform = CreatePlatform();
            var window = await OpenWindowAsync(Configure(platform)).ConfigureAwait(false);

            var handles = await window.ShowOpenFilePicker().ConfigureAwait(false);

            Assert.AreEqual(1, handles.Length);
            Assert.AreEqual(FileSystemHandleKind.File, handles[0].Kind);
            Assert.AreEqual("a.txt", handles[0].Name);
            Assert.AreEqual("A", await ReadAsync(handles[0]).ConfigureAwait(false));
        }

        [Test]
        public async Task OpenPickerReceivesTheOriginAndTheOptions()
        {
            var seen = default(OpenFilePickerOptions);
            var platform = CreatePlatform();
            platform.OpenFilePicker = o => { seen = o; return new[] { "b.txt" }; };
            var window = await OpenWindowAsync(Configure(platform)).ConfigureAwait(false);
            var options = new OpenFilePickerOptions
            {
                Id = "docs",
                StartIn = "documents",
                Types = new[]
                {
                    new FilePickerAcceptType
                    {
                        Description = "Text",
                        Accept = new Dictionary<String, FilePickerSuffixes> { ["text/plain"] = new[] { ".txt", ".tar.gz" }, ["text/markdown"] = ".md" },
                    },
                },
            };

            var handles = await window.ShowOpenFilePicker(options).ConfigureAwait(false);

            Assert.AreSame(options, seen);
            CollectionAssert.AreEqual(new[] { ".txt", ".tar.gz" }, seen.Types[0].Accept["text/plain"].Values);
            CollectionAssert.AreEqual(new[] { ".md" }, seen.Types[0].Accept["text/markdown"].Values);
            Assert.AreEqual("b.txt", handles[0].Name);
        }

        [Test]
        public async Task OnlyAMultiplePickerReturnsMultipleFiles()
        {
            var platform = CreatePlatform();
            platform.OpenFilePicker = _ => new[] { "a.txt", "b.txt" };
            var window = await OpenWindowAsync(Configure(platform)).ConfigureAwait(false);

            var single = await window.ShowOpenFilePicker().ConfigureAwait(false);
            var multiple = await window.ShowOpenFilePicker(new OpenFilePickerOptions { Multiple = true }).ConfigureAwait(false);

            Assert.AreEqual(1, single.Length);
            Assert.AreEqual(2, multiple.Length);
        }

        [Test]
        public async Task HandlesToTheSameFileAreTheSameEntry()
        {
            var platform = CreatePlatform();
            platform.OpenFilePicker = _ => new[] { "a.txt", "b.txt" };
            var window = await OpenWindowAsync(Configure(platform)).ConfigureAwait(false);
            var options = new OpenFilePickerOptions { Multiple = true };

            var first = await window.ShowOpenFilePicker(options).ConfigureAwait(false);
            var second = await window.ShowOpenFilePicker(options).ConfigureAwait(false);
            var saved = await window.ShowSaveFilePicker(new SaveFilePickerOptions { SuggestedName = "a.txt" }).ConfigureAwait(false);

            Assert.AreNotSame(first[0], second[0]);
            Assert.IsTrue(await first[0].IsSameEntry(second[0]).ConfigureAwait(false));
            Assert.IsTrue(await first[0].IsSameEntry(saved).ConfigureAwait(false));
            Assert.IsFalse(await first[0].IsSameEntry(first[1]).ConfigureAwait(false));
            Assert.IsFalse(await first[0].IsSameEntry(null).ConfigureAwait(false));
        }

        [Test]
        public async Task HandlesOfDifferentPlatformsAreNotTheSameEntry()
        {
            var first = await OpenWindowAsync(Configure(CreatePlatform())).ConfigureAwait(false);
            var second = await OpenWindowAsync(Configure(CreatePlatform())).ConfigureAwait(false);

            var a = (await first.ShowOpenFilePicker().ConfigureAwait(false))[0];
            var b = (await second.ShowOpenFilePicker().ConfigureAwait(false))[0];

            Assert.IsFalse(await a.IsSameEntry(b).ConfigureAwait(false));
        }

        [Test]
        public async Task DismissingAPickerRejectsWithAnAbortError()
        {
            var platform = CreatePlatform();
            platform.OpenFilePicker = _ => Array.Empty<String>();
            platform.SaveFilePicker = _ => null;
            var window = await OpenWindowAsync(Configure(platform)).ConfigureAwait(false);

            AssertRejected(DomError.Abort, () => window.ShowOpenFilePicker());
            AssertRejected(DomError.Abort, () => window.ShowSaveFilePicker());
        }

        [Test]
        public async Task SaveCreatesANewFile()
        {
            var platform = new MemoryFileSystemPlatform();
            var window = await OpenWindowAsync(Configure(platform)).ConfigureAwait(false);

            var handle = await window.ShowSaveFilePicker(new SaveFilePickerOptions { SuggestedName = "new.txt" }).ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { "new.txt" }, platform.FileNames);
            Assert.AreEqual(String.Empty, await ReadAsync(handle).ConfigureAwait(false));

            await WriteAsync(handle, "saved").ConfigureAwait(false);

            Assert.AreEqual("saved", GetText(platform, "new.txt"));
            Assert.AreEqual("saved", await ReadAsync(handle).ConfigureAwait(false));
        }

        [Test]
        public async Task SavingOverwritesAnExistingFile()
        {
            var platform = CreatePlatform();
            var window = await OpenWindowAsync(Configure(platform)).ConfigureAwait(false);

            var handle = await window.ShowSaveFilePicker(new SaveFilePickerOptions { SuggestedName = "a.txt" }).ConfigureAwait(false);

            Assert.AreEqual("A", GetText(platform, "a.txt"));

            await WriteAsync(handle, "other").ConfigureAwait(false);

            Assert.AreEqual("other", GetText(platform, "a.txt"));
        }

        [Test]
        public async Task WritesOnlyReachTheFileWhenClosing()
        {
            var platform = CreatePlatform();
            var handle = await OpenFirstAsync(platform).ConfigureAwait(false);
            var stream = await handle.CreateWritable().ConfigureAwait(false);

            await stream.Write("changed").ConfigureAwait(false);

            Assert.AreEqual("A", GetText(platform, "a.txt"));
            Assert.AreEqual("A", await ReadAsync(handle).ConfigureAwait(false));

            await stream.Close().ConfigureAwait(false);

            Assert.AreEqual("changed", GetText(platform, "a.txt"));
        }

        [Test]
        public async Task AbortingDiscardsTheWrites()
        {
            var platform = CreatePlatform();
            var handle = await OpenFirstAsync(platform).ConfigureAwait(false);
            var stream = await handle.CreateWritable().ConfigureAwait(false);

            await stream.Write("changed").ConfigureAwait(false);
            await stream.Abort().ConfigureAwait(false);
            await stream.Abort("again").ConfigureAwait(false);

            Assert.AreEqual("A", GetText(platform, "a.txt"));
            Assert.ThrowsAsync<InvalidOperationException>(() => stream.Close());
        }

        [Test]
        public async Task AFailingCommitLeavesTheFileUntouched()
        {
            var inner = CreatePlatform();
            var platform = new FailingPlatform(inner);
            var window = await OpenWindowAsync(Configure(platform)).ConfigureAwait(false);
            var handle = (await window.ShowOpenFilePicker().ConfigureAwait(false))[0];
            var stream = await handle.CreateWritable().ConfigureAwait(false);
            await stream.Write("changed").ConfigureAwait(false);

            Assert.ThrowsAsync<IOException>(() => stream.Close());

            Assert.AreEqual("A", GetText(inner, "a.txt"));
            Assert.ThrowsAsync<InvalidOperationException>(() => stream.Write("again"));
        }

        [Test]
        public async Task AWritableStartsEmptyUnlessKeepingTheExistingData()
        {
            var platform = CreatePlatform();
            platform.SetFile("a.txt", Encoding.UTF8.GetBytes("Hello"));
            var handle = await OpenFirstAsync(platform).ConfigureAwait(false);

            var keeping = await handle.CreateWritable(new FileSystemCreateWritableOptions { KeepExistingData = true }).ConfigureAwait(false);
            await keeping.Write("J").ConfigureAwait(false);
            await keeping.Close().ConfigureAwait(false);

            Assert.AreEqual("Jello", GetText(platform, "a.txt"));

            var fresh = await handle.CreateWritable().ConfigureAwait(false);
            await fresh.Close().ConfigureAwait(false);

            Assert.AreEqual(String.Empty, GetText(platform, "a.txt"));
        }

        [Test]
        public async Task WriteAcceptsStringsBytesAndBlobs()
        {
            var platform = CreatePlatform();
            var handle = await OpenFirstAsync(platform).ConfigureAwait(false);
            var stream = await handle.CreateWritable().ConfigureAwait(false);

            await stream.Write("a").ConfigureAwait(false);
            await stream.Write(new Byte[] { (Byte)'b' }).ConfigureAwait(false);
            await stream.Write(new InputFile("c.txt", "text/plain", Encoding.UTF8.GetBytes("c"))).ConfigureAwait(false);
            await stream.Write(new WriteParams { Type = WriteCommandType.Write, Data = "d" }).ConfigureAwait(false);
            await stream.Close().ConfigureAwait(false);

            Assert.AreEqual("abcd", GetText(platform, "a.txt"));
        }

        [Test]
        public async Task SeekAndTruncateMoveThePositionAndResizeTheFile()
        {
            var platform = CreatePlatform();
            var handle = await OpenFirstAsync(platform).ConfigureAwait(false);
            var stream = await handle.CreateWritable().ConfigureAwait(false);

            await stream.Write("Hello World").ConfigureAwait(false);
            await stream.Seek(0).ConfigureAwait(false);
            await stream.Write("J").ConfigureAwait(false);
            await stream.Write(new WriteParams { Type = WriteCommandType.Write, Position = 6, Data = "w" }).ConfigureAwait(false);
            await stream.Truncate(8).ConfigureAwait(false);
            await stream.Write(new WriteParams { Type = WriteCommandType.Seek, Position = 8 }).ConfigureAwait(false);
            await stream.Write("!").ConfigureAwait(false);
            await stream.Close().ConfigureAwait(false);

            Assert.AreEqual("Jello wo!", GetText(platform, "a.txt"));
        }

        [Test]
        public async Task TruncatingMovesAPositionBehindTheNewEnd()
        {
            var platform = CreatePlatform();
            var handle = await OpenFirstAsync(platform).ConfigureAwait(false);
            var stream = await handle.CreateWritable().ConfigureAwait(false);

            await stream.Write("Hello").ConfigureAwait(false);
            await stream.Truncate(2).ConfigureAwait(false);
            await stream.Write("y").ConfigureAwait(false);
            await stream.Close().ConfigureAwait(false);

            Assert.AreEqual("Hey", GetText(platform, "a.txt"));
        }

        [Test]
        public async Task WritingAndTruncatingBehindTheEndFillWithZeros()
        {
            var platform = CreatePlatform();
            var handle = await OpenFirstAsync(platform).ConfigureAwait(false);
            var stream = await handle.CreateWritable().ConfigureAwait(false);

            await stream.Seek(1).ConfigureAwait(false);
            await stream.Write("a").ConfigureAwait(false);
            await stream.Truncate(3).ConfigureAwait(false);
            await stream.Close().ConfigureAwait(false);

            CollectionAssert.AreEqual(new Byte[] { 0, (Byte)'a', 0 }, platform.GetFile("a.txt"));
        }

        [Test]
        public async Task CommandsWithoutTheirMembersAreTypeErrors()
        {
            var handle = await OpenFirstAsync(CreatePlatform()).ConfigureAwait(false);
            var stream = await handle.CreateWritable().ConfigureAwait(false);

            Assert.ThrowsAsync<ArgumentException>(() => stream.Write((Object)null));
            Assert.ThrowsAsync<ArgumentException>(() => stream.Write(new WriteParams { Type = WriteCommandType.Write }));
            Assert.ThrowsAsync<ArgumentException>(() => stream.Write(new WriteParams { Type = WriteCommandType.Seek }));
            Assert.ThrowsAsync<ArgumentException>(() => stream.Write(new WriteParams { Type = WriteCommandType.Truncate }));
        }

        [Test]
        public async Task PositionsExceedingTheBufferAreAQuotaError()
        {
            var handle = await OpenFirstAsync(CreatePlatform()).ConfigureAwait(false);
            var stream = await handle.CreateWritable().ConfigureAwait(false);

            AssertRejected(DomError.QuotaExceeded, () => stream.Seek(Double.MaxValue));
        }

        [TestCase(-1.0)]
        [TestCase(Double.NaN)]
        public async Task InvalidPositionsAreTypeErrors(Double position)
        {
            var handle = await OpenFirstAsync(CreatePlatform()).ConfigureAwait(false);
            var stream = await handle.CreateWritable().ConfigureAwait(false);

            Assert.ThrowsAsync<ArgumentException>(() => stream.Seek(position));
            Assert.ThrowsAsync<ArgumentException>(() => stream.Truncate(position));
        }

        [Test]
        public async Task DataThatCannotBeConvertedIsRejectedInsteadOfBeingWrittenAsText()
        {
            var platform = CreatePlatform();
            var handle = await OpenFirstAsync(platform).ConfigureAwait(false);
            var stream = await handle.CreateWritable().ConfigureAwait(false);

            Assert.ThrowsAsync<ArgumentException>(() => stream.Write(new Object()));
            Assert.ThrowsAsync<ArgumentException>(() => stream.Write(new WriteParams { Type = WriteCommandType.Write, Data = new Object() }));

            await stream.Close().ConfigureAwait(false);

            Assert.AreEqual(String.Empty, GetText(platform, "a.txt"));
        }

        [Test]
        public async Task WriteOverloadsForScriptsReachTheStream()
        {
            var platform = CreatePlatform();
            var handle = await OpenFirstAsync(platform).ConfigureAwait(false);
            var stream = await handle.CreateWritable().ConfigureAwait(false);

            await FileSystemWritableFileStreamExtensions.Write(stream, "a").ConfigureAwait(false);
            await FileSystemWritableFileStreamExtensions.Write(stream, new InputFile("b", String.Empty, Encoding.UTF8.GetBytes("b"))).ConfigureAwait(false);
            await FileSystemWritableFileStreamExtensions.Write(stream, new WriteParams { Type = WriteCommandType.Write, Data = "c" }).ConfigureAwait(false);
            await FileSystemWritableFileStreamExtensions.Write(stream, new Byte[] { (Byte)'d' }).ConfigureAwait(false);
            await stream.Close().ConfigureAwait(false);

            Assert.AreEqual("abcd", GetText(platform, "a.txt"));
        }

        [Test]
        public async Task AClosedStreamRejectsFurtherOperations()
        {
            var handle = await OpenFirstAsync(CreatePlatform()).ConfigureAwait(false);
            var stream = await handle.CreateWritable().ConfigureAwait(false);
            await stream.Close().ConfigureAwait(false);

            Assert.ThrowsAsync<InvalidOperationException>(() => stream.Write("x"));
            Assert.ThrowsAsync<InvalidOperationException>(() => stream.Seek(0));
            Assert.ThrowsAsync<InvalidOperationException>(() => stream.Truncate(0));
            Assert.ThrowsAsync<InvalidOperationException>(() => stream.Close());
        }

        [Test]
        public async Task GetFileOfARemovedFileIsNotFound()
        {
            var platform = new FailingPlatform(CreatePlatform()) { FileExists = false };
            var window = await OpenWindowAsync(Configure(platform)).ConfigureAwait(false);
            var handle = (await window.ShowOpenFilePicker().ConfigureAwait(false))[0];

            AssertRejected(DomError.NotFound, () => handle.GetFile());
        }

        [Test]
        public async Task ADeniedPermissionRejectsThePickers()
        {
            var policy = new PermissionPolicy(PermissionState.Denied);
            var picks = 0;
            var platform = CreatePlatform();
            platform.OpenFilePicker = _ => { picks++; return new[] { "a.txt" }; };
            platform.SaveFilePicker = _ => { picks++; return "a.txt"; };
            var window = await OpenWindowAsync(Configure(platform, policy)).ConfigureAwait(false);

            AssertNotAllowed(() => window.ShowOpenFilePicker());
            AssertNotAllowed(() => window.ShowSaveFilePicker());
            Assert.AreEqual(0, picks);
        }

        [Test]
        public async Task AnUnansweredPromptRejectsThePickers()
        {
            var window = await OpenWindowAsync(Configure(CreatePlatform(), new PermissionPolicy())).ConfigureAwait(false);

            AssertNotAllowed(() => window.ShowOpenFilePicker());
        }

        [Test]
        public async Task PermissionsAreRequestedPerOriginAndMode()
        {
            var prompts = new List<String>();
            var policy = new PermissionPolicy
            {
                PromptHandler = (origin, name) =>
                {
                    prompts.Add(origin + "|" + name);
                    return Task.FromResult(PermissionState.Granted);
                },
            };
            var window = await OpenWindowAsync(Configure(CreatePlatform(), policy)).ConfigureAwait(false);

            var handle = (await window.ShowOpenFilePicker().ConfigureAwait(false))[0];
            await handle.GetFile().ConfigureAwait(false);
            await WriteAsync(handle, "x").ConfigureAwait(false);

            CollectionAssert.AreEqual(new[] { Origin + "|file-system-read", Origin + "|file-system-write" }, prompts);
        }

        [Test]
        public async Task ReadAccessDoesNotAllowWriting()
        {
            var platform = CreatePlatform();
            var policy = new PermissionPolicy(PermissionState.Denied);
            policy.Set(PermissionNames.FileSystemRead, PermissionState.Granted);
            var window = await OpenWindowAsync(Configure(platform, policy)).ConfigureAwait(false);

            var handle = (await window.ShowOpenFilePicker().ConfigureAwait(false))[0];

            Assert.AreEqual("A", await ReadAsync(handle).ConfigureAwait(false));
            AssertNotAllowed(() => handle.CreateWritable());
            Assert.AreEqual("A", GetText(platform, "a.txt"));
        }

        [Test]
        public async Task WriteAccessIncludesReading()
        {
            var policy = new PermissionPolicy(PermissionState.Denied);
            policy.Set(PermissionNames.FileSystemWrite, PermissionState.Granted);
            var window = await OpenWindowAsync(Configure(CreatePlatform(), policy)).ConfigureAwait(false);

            var handle = await window.ShowSaveFilePicker(new SaveFilePickerOptions { SuggestedName = "a.txt" }).ConfigureAwait(false);

            Assert.AreEqual("A", await ReadAsync(handle).ConfigureAwait(false));
            Assert.AreEqual(PermissionState.Granted, await handle.QueryPermission().ConfigureAwait(false));
        }

        [Test]
        public async Task RevokingThePermissionStopsAnOpenWritableWithoutTouchingTheFile()
        {
            var platform = CreatePlatform();
            var policy = new PermissionPolicy(PermissionState.Granted);
            var window = await OpenWindowAsync(Configure(platform, policy)).ConfigureAwait(false);
            var handle = (await window.ShowOpenFilePicker().ConfigureAwait(false))[0];
            var stream = await handle.CreateWritable().ConfigureAwait(false);
            var other = await handle.CreateWritable().ConfigureAwait(false);
            await stream.Write("changed").ConfigureAwait(false);
            await other.Write("changed").ConfigureAwait(false);

            policy.Set(PermissionNames.FileSystemWrite, PermissionState.Denied);

            AssertNotAllowed(() => stream.Write("more"));
            AssertNotAllowed(() => other.Close());
            Assert.AreEqual("A", GetText(platform, "a.txt"));
        }

        [Test]
        public async Task HandlesQueryAndRequestPermissionsByMode()
        {
            var policy = new PermissionPolicy { PromptHandler = (origin, name) => Task.FromResult(PermissionState.Denied) };
            policy.Set(PermissionNames.FileSystemRead, PermissionState.Granted);
            var window = await OpenWindowAsync(Configure(CreatePlatform(), policy)).ConfigureAwait(false);
            var handle = (await window.ShowOpenFilePicker().ConfigureAwait(false))[0];
            var write = new FileSystemHandlePermissionDescriptor { Mode = FileSystemPermissionMode.ReadWrite };

            Assert.AreEqual(PermissionState.Granted, await handle.QueryPermission().ConfigureAwait(false));
            Assert.AreEqual(PermissionState.Prompt, await handle.QueryPermission(write).ConfigureAwait(false));
            Assert.AreEqual(PermissionState.Granted, await handle.RequestPermission().ConfigureAwait(false));
            Assert.AreEqual(PermissionState.Denied, await handle.RequestPermission(write).ConfigureAwait(false));
            Assert.AreEqual(PermissionState.Denied, await handle.QueryPermission(write).ConfigureAwait(false));
        }

        [Test]
        public async Task WithoutPolicyHandlesAreNotGated()
        {
            var window = await OpenWindowAsync(Configure(CreatePlatform())).ConfigureAwait(false);
            var handle = (await window.ShowOpenFilePicker().ConfigureAwait(false))[0];
            var write = new FileSystemHandlePermissionDescriptor { Mode = FileSystemPermissionMode.ReadWrite };

            Assert.AreEqual(PermissionState.Granted, await handle.QueryPermission(write).ConfigureAwait(false));
            Assert.AreEqual(PermissionState.Granted, await handle.RequestPermission(write).ConfigureAwait(false));
        }

        [TestCase("http://files.example/")]
        [TestCase("about:blank")]
        public async Task PickersRequireASecureContextByDefault(String address)
        {
            var window = await OpenWindowAsync(Configure(CreatePlatform()), address).ConfigureAwait(false);

            AssertRejected(DomError.Security, () => window.ShowOpenFilePicker());
            AssertRejected(DomError.Security, () => window.ShowSaveFilePicker());
        }

        [TestCase("http://localhost:8080/")]
        [TestCase("http://127.0.0.1/")]
        public async Task PickersWorkOnLoopbackAddresses(String address)
        {
            var window = await OpenWindowAsync(Configure(CreatePlatform()), address).ConfigureAwait(false);

            Assert.AreEqual(1, (await window.ShowOpenFilePicker().ConfigureAwait(false)).Length);
        }

        [Test]
        public async Task TheSecureContextRequirementIsConfigurable()
        {
            var options = new FileSystemAccessOptions { RequireSecureContext = false };
            var config = Configuration.Default.WithNavigator().WithFileSystemAccess(CreatePlatform(), options);
            var window = await OpenWindowAsync(config, "http://files.example/").ConfigureAwait(false);

            Assert.AreEqual(1, (await window.ShowOpenFilePicker().ConfigureAwait(false)).Length);
        }

        [TestCase("not a mime type", ".txt")]
        [TestCase("text/plain;charset=utf-8", ".txt")]
        [TestCase("text", ".txt")]
        [TestCase("text/plain", "txt")]
        [TestCase("text/plain", ".")]
        [TestCase("text/plain", ".tx t")]
        [TestCase("text/plain", ".txt.")]
        [TestCase("text/plain", ".waytoolongsuffix1")]
        public async Task InvalidAcceptTypesAreTypeErrors(String type, String suffix)
        {
            var window = await OpenWindowAsync(Configure(CreatePlatform())).ConfigureAwait(false);
            var options = new OpenFilePickerOptions
            {
                Types = new[]
                {
                    new FilePickerAcceptType { Accept = new Dictionary<String, FilePickerSuffixes> { [type] = new[] { suffix } } },
                },
            };

            Assert.ThrowsAsync<ArgumentException>(() => window.ShowOpenFilePicker(options));
        }

        [Test]
        public async Task ASingleInvalidSuffixIsATypeError()
        {
            var window = await OpenWindowAsync(Configure(CreatePlatform())).ConfigureAwait(false);
            var options = new OpenFilePickerOptions
            {
                Types = new[]
                {
                    new FilePickerAcceptType { Accept = new Dictionary<String, FilePickerSuffixes> { ["text/plain"] = "txt" } },
                },
            };

            Assert.ThrowsAsync<ArgumentException>(() => window.ShowOpenFilePicker(options));
        }

        [Test]
        public async Task OtherInvalidPickerOptionsAreTypeErrors()
        {
            var window = await OpenWindowAsync(Configure(CreatePlatform())).ConfigureAwait(false);

            Assert.ThrowsAsync<ArgumentException>(() => window.ShowOpenFilePicker(new OpenFilePickerOptions { ExcludeAcceptAllOption = true }));
            Assert.ThrowsAsync<ArgumentException>(() => window.ShowOpenFilePicker(new OpenFilePickerOptions { Types = new[] { new FilePickerAcceptType() } }));
            Assert.ThrowsAsync<ArgumentException>(() => window.ShowSaveFilePicker(new SaveFilePickerOptions { Id = "not valid" }));
            Assert.ThrowsAsync<ArgumentException>(() => window.ShowSaveFilePicker(new SaveFilePickerOptions { Id = new String('a', 33) }));
        }

        [Test]
        public void EntriesRequireAnIdentity()
        {
            Assert.Throws<ArgumentException>(() => new FileSystemEntry(null, "name"));
            Assert.Throws<ArgumentException>(() => new FileSystemEntry(String.Empty, "name"));
            Assert.AreEqual(String.Empty, new FileSystemEntry("id", null).Name);
        }

        [Test]
        public void FileSystemAccessCanBeRegisteredViaIoOptions()
        {
            var platform = new MemoryFileSystemPlatform();
            var options = new FileSystemAccessOptions { RequireSecureContext = false };
            var config = Configuration.Default.WithIo(new IoOptions { FileSystemPlatform = platform, FileSystemAccess = options });

            CollectionAssert.Contains(config.Services, platform);
            CollectionAssert.Contains(config.Services, options);
        }

        [Test]
        public void NoPlatformLeavesTheConfigurationUnchanged()
        {
            var config = Configuration.Default.WithFileSystemAccess(null);

            Assert.IsFalse(config.Services.OfType<FileSystemAccessOptions>().Any());
        }

        private static MemoryFileSystemPlatform CreatePlatform()
        {
            var platform = new MemoryFileSystemPlatform();
            platform.SetFile("a.txt", Encoding.UTF8.GetBytes("A"), "text/plain");
            platform.SetFile("b.txt", Encoding.UTF8.GetBytes("B"), "text/plain");
            return platform;
        }

        private static IConfiguration Configure(IFileSystemPlatform platform, IPermissionPolicy policy = null) =>
            Configuration.Default.WithNavigator().WithPermissions(policy).WithFileSystemAccess(platform);

        private static async Task<IWindow> OpenWindowAsync(IConfiguration configuration, String address = Address)
        {
            var context = BrowsingContext.New(configuration);
            var document = await context.OpenAsync(res => res.Address(address).Content("<!doctype html>")).ConfigureAwait(false);
            return document.DefaultView;
        }

        private static async Task<FileSystemFileHandle> OpenFirstAsync(MemoryFileSystemPlatform platform)
        {
            var window = await OpenWindowAsync(Configure(platform)).ConfigureAwait(false);
            return (await window.ShowOpenFilePicker().ConfigureAwait(false))[0];
        }

        private static async Task<String> ReadAsync(FileSystemFileHandle handle)
        {
            var file = await handle.GetFile().ConfigureAwait(false);

            using (var reader = new StreamReader(file.Body))
            {
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
        }

        private static async Task WriteAsync(FileSystemFileHandle handle, String content)
        {
            var stream = await handle.CreateWritable().ConfigureAwait(false);
            await stream.Write(content).ConfigureAwait(false);
            await stream.Close().ConfigureAwait(false);
        }

        private static String GetText(MemoryFileSystemPlatform platform, String name) =>
            Encoding.UTF8.GetString(platform.GetFile(name));

        private static void AssertRejected(DomError error, AsyncTestDelegate action)
        {
            var ex = Assert.ThrowsAsync<DomException>(action);
            Assert.AreEqual((Int32)error, ex.Code);
        }

        private static void AssertNotAllowed(AsyncTestDelegate action)
        {
            var ex = Assert.ThrowsAsync<DomException>(action);
            Assert.AreEqual("NotAllowedError", ex.Name);
        }

        private sealed class FailingPlatform : IFileSystemPlatform
        {
            private readonly IFileSystemPlatform _inner;

            public FailingPlatform(IFileSystemPlatform inner)
            {
                _inner = inner;
            }

            public Boolean FileExists { get; set; } = true;

            public Task<FileSystemEntry[]> PickFilesAsync(String origin, OpenFilePickerOptions options) => _inner.PickFilesAsync(origin, options);

            public Task<FileSystemEntry> PickSaveFileAsync(String origin, SaveFilePickerOptions options) => _inner.PickSaveFileAsync(origin, options);

            public Task<IFile> ReadFileAsync(FileSystemEntry entry) => FileExists ? _inner.ReadFileAsync(entry) : Task.FromResult<IFile>(null);

            public Task WriteFileAsync(FileSystemEntry entry, Stream content) => Task.FromException(new IOException());
        }
    }
}
