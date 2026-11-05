namespace AngleSharp.Io.Tests.IndexedDb
{
    using AngleSharp;
    using AngleSharp.Browser;
    using AngleSharp.Common;
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The IndexedDB of script: requests are done later and tell so by events.
    /// </summary>
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    public class IdbTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        //  Everything of a test happens on its thread, one task after the other, as for a script.
        private sealed class ManualLoop : IEventLoop
        {
            private readonly Queue<Action<CancellationToken>> _tasks = new Queue<Action<CancellationToken>>();

            public ICancellable Enqueue(Action<CancellationToken> action, TaskPriority priority)
            {
                lock (_tasks)
                {
                    _tasks.Enqueue(action);
                }

                return null;
            }

            public void Spin()
            {
            }

            public void CancelAll()
            {
            }

            public Boolean RunOne()
            {
                Action<CancellationToken> next;

                lock (_tasks)
                {
                    if (_tasks.Count == 0)
                    {
                        return false;
                    }

                    next = _tasks.Dequeue();
                }

                next.Invoke(CancellationToken.None);
                return true;
            }
        }

        private readonly ManualLoop _loop = new ManualLoop();

        private async Task<T> Within<T>(Task<T> task)
        {
            var deadline = DateTime.UtcNow + Timeout;

            while (!task.IsCompleted)
            {
                if (!_loop.RunOne())
                {
                    Assert.Less(DateTime.UtcNow, deadline, "Timed out waiting for an event.");
                    await Task.Delay(1);
                }
            }

            return await task;
        }

        private async Task<IWindow> OpenWindowAsync(String address = "https://idb.example/")
        {
            var config = Configuration.Default.WithOnly<IEventLoop>(_loop);
            var document = await BrowsingContext.New(config).OpenAsync(m => m.Address(address).Content("<!doctype html>"));
            return document.DefaultView;
        }

        private Task<IdbRequest> Done(IdbRequest request)
        {
            var completion = new TaskCompletionSource<IdbRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            request.Succeeded += (_, _) => completion.TrySetResult(request);
            request.Failed += (_, _) => completion.TrySetResult(request);
            return Within(completion.Task);
        }

        private Task<IdbTransaction> Finished(IdbTransaction transaction)
        {
            var completion = new TaskCompletionSource<IdbTransaction>(TaskCreationOptions.RunContinuationsAsynchronously);
            transaction.Completed += (_, _) => completion.TrySetResult(transaction);
            transaction.Aborted += (_, _) => completion.TrySetResult(transaction);
            return Within(completion.Task);
        }

        private async Task<IdbDatabase> OpenAsync(IWindow window, String name, Double? version, Action<IdbDatabase, IdbTransaction> upgrade)
        {
            var request = window.IndexedDbOfScript().Open(name, version);
            request.UpgradeNeeded += (_, _) => upgrade?.Invoke((IdbDatabase)request.Result, request.Transaction);
            await Done(request);
            Assert.IsNull(request.Error, request.Error?.ToString());
            return (IdbDatabase)request.Result;
        }

        private Task<IdbDatabase> OpenStoreAsync(IWindow window, String name = "db") =>
            OpenAsync(window, name, 1, (db, _) => db.CreateObjectStore("s"));

        private async Task<Object> Get(IdbDatabase db, Object key) =>
            (await Done(db.Transaction("s").ObjectStore("s").Get(key))).Result;

        private Task<IdbRequest> In(IdbDatabase db, Func<IdbObjectStore, IdbRequest> make, String name = "s") =>
            Done(make.Invoke(db.Transaction(name, "readwrite").ObjectStore(name)));

        private static Dictionary<String, Object> Record(params (String Key, Object Value)[] entries) =>
            entries.ToDictionary(m => m.Key, m => m.Value);

        [Test]
        public async Task RequestsAreDoneLaterSoThatHandlersCanBeAdded()
        {
            var window = await OpenWindowAsync();
            var request = window.IndexedDbOfScript().Open("db");

            Assert.AreEqual("pending", request.ReadyState);
            Assert.Throws<DomException>(() => { _ = request.Result; });

            await Done(request);

            Assert.AreEqual("done", request.ReadyState);
            Assert.IsInstanceOf<IdbDatabase>(request.Result);
        }

        [Test]
        public async Task OpeningNewDatabaseAsksForTheUpgradeFirst()
        {
            var window = await OpenWindowAsync();
            var log = new List<String>();
            var request = window.IndexedDbOfScript().Open("db");
            request.UpgradeNeeded += (_, ev) =>
            {
                var change = (IdbVersionChangeEvent)ev;
                log.Add($"upgrade {change.OldVersion}->{change.NewVersion} {request.Transaction.Mode}");
                ((IdbDatabase)request.Result).CreateObjectStore("s");
            };
            request.Succeeded += (_, _) => log.Add("success");

            await Done(request);

            Assert.AreEqual(new[] { "upgrade 0->1 versionchange", "success" }, log);
            var db = (IdbDatabase)request.Result;
            Assert.AreEqual(1, db.Version);
            Assert.IsTrue(db.ObjectStoreNames.Contains("s"));
            Assert.IsNull(request.Transaction);
        }

        [Test]
        public async Task StoresCanOnlyBeCreatedDuringAnUpgrade()
        {
            var db = await OpenStoreAsync(await OpenWindowAsync());

            Assert.Throws<DomException>(() => db.CreateObjectStore("t"));
        }

        [Test]
        public async Task LowerVersionFails()
        {
            var window = await OpenWindowAsync();
            var db = await OpenAsync(window, "db", 3, null);
            db.Close();

            var request = window.IndexedDbOfScript().Open("db", 2);
            await Done(request);

            Assert.AreEqual("VersionError", ((DomException)request.Error).Name);
        }

        [Test]
        public async Task AnUpgradeNotifiesAndWaitsForOtherConnections()
        {
            var window = await OpenWindowAsync();
            var db = await OpenAsync(window, "db", 1, null);
            var log = new List<String>();
            db.VersionChanged += (_, _) => log.Add("versionchange");

            var blocked = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
            var request = window.IndexedDbOfScript().Open("db", 2);
            request.Blocked += (_, _) =>
            {
                log.Add("blocked");
                blocked.TrySetResult(true);
            };
            await Within(blocked.Task);
            Assert.AreEqual("pending", request.ReadyState);

            db.Close();
            await Done(request);

            Assert.AreEqual(new[] { "versionchange", "blocked" }, log);
            Assert.AreEqual(2, ((IdbDatabase)request.Result).Version);
        }

        [Test]
        public async Task ValuesAreFoundAgain()
        {
            var db = await OpenStoreAsync(await OpenWindowAsync());
            var value = Record(("a", 1.0));
            var tx = db.Transaction("s", "readwrite");

            var put = await Done(tx.ObjectStore("s").Put(value, "k"));

            Assert.AreEqual("k", put.Result);
            await Finished(tx);
            Assert.AreSame(value, await Get(db, "k"));
            Assert.IsNull(await Get(db, "missing"));
        }

        [Test]
        public async Task ADatabaseIsSeenByTheWindowsOfAnOriginOnly()
        {
            var window = await OpenWindowAsync();
            var db = await OpenStoreAsync(window);
            await Done(db.Transaction("s", "readwrite").ObjectStore("s").Put("v", "k"));
            db.Close();

            var other = window.IndexedDbOfScript();
            var databases = await other.Databases();
            Assert.AreEqual("db", ((IDictionary<String, Object>)databases[0])["name"]);

            var foreign = await OpenWindowAsync("https://other.example/");
            Assert.IsEmpty(await foreign.IndexedDbOfScript().Databases());
        }

        [Test]
        public async Task ReadOnlyTransactionCanNotWrite()
        {
            var db = await OpenStoreAsync(await OpenWindowAsync());
            var store = db.Transaction("s").ObjectStore("s");

            var error = Assert.Throws<DomException>(() => store.Put("v", "k"));

            Assert.AreEqual("ReadOnlyError", error.Name);
        }

        [Test]
        public async Task TransactionIsInactiveLaterOn()
        {
            var db = await OpenStoreAsync(await OpenWindowAsync());
            var tx = db.Transaction("s");
            var store = tx.ObjectStore("s");

            await Finished(tx);

            var error = Assert.Throws<DomException>(() => store.Get("k"));
            Assert.AreEqual("TransactionInactiveError", error.Name);
        }

        [Test]
        public async Task TransactionTakesRequestsDuringTheEventOfARequest()
        {
            var db = await OpenStoreAsync(await OpenWindowAsync());
            var tx = db.Transaction("s", "readwrite");
            var store = tx.ObjectStore("s");
            var first = store.Put("a", "1");
            IdbRequest second = null;
            first.Succeeded += (_, _) => second = store.Put("b", "2");

            await Finished(tx);

            Assert.IsNotNull(second);
            Assert.AreEqual("done", second.ReadyState);
            Assert.AreEqual("b", await Get(db, "2"));
        }

        [Test]
        public async Task EmptyTransactionCompletesByItself()
        {
            var db = await OpenStoreAsync(await OpenWindowAsync());

            var tx = await Finished(db.Transaction("s"));

            Assert.IsNull(tx.Error);
        }

        [Test]
        public async Task AbortUndoesWhatWasWritten()
        {
            var db = await OpenStoreAsync(await OpenWindowAsync());
            var setup = db.Transaction("s", "readwrite");
            setup.ObjectStore("s").Put("old", "kept");
            setup.ObjectStore("s").Put("gone", "removed");
            await Finished(setup);

            var tx = db.Transaction("s", "readwrite");
            var store = tx.ObjectStore("s");
            store.Put("new", "kept");
            store.Delete("removed");
            var last = store.Put("x", "added");
            last.Succeeded += (_, _) => tx.Abort();
            await Finished(tx);

            Assert.AreEqual("old", await Get(db, "kept"));
            Assert.AreEqual("gone", await Get(db, "removed"));
            Assert.IsNull(await Get(db, "added"));
        }

        [Test]
        public async Task PendingRequestsOfAnAbortedTransactionFail()
        {
            var db = await OpenStoreAsync(await OpenWindowAsync());
            var tx = db.Transaction("s", "readwrite");
            var request = tx.ObjectStore("s").Put("v", "k");

            tx.Abort();

            await Done(request);
            Assert.AreEqual((Int32)DomError.Abort, ((DomException)request.Error).Code);
            Assert.IsNull(await Get(db, "k"));
        }

        [Test]
        public async Task AFailedRequestAbortsTheTransactionUnlessPrevented()
        {
            var db = await OpenStoreAsync(await OpenWindowAsync());
            var setup = db.Transaction("s", "readwrite");
            setup.ObjectStore("s").Put("v", "k");
            await Finished(setup);

            var tx = db.Transaction("s", "readwrite");
            tx.ObjectStore("s").Put("first", "other");
            var failing = tx.ObjectStore("s").Add("v", "k");
            await Finished(tx);

            Assert.AreEqual("ConstraintError", ((DomException)failing.Error).Name);
            Assert.AreEqual("ConstraintError", ((DomException)tx.Error).Name);
            Assert.IsNull(await Get(db, "other"));

            var kept = db.Transaction("s", "readwrite");
            kept.ObjectStore("s").Put("first", "other");
            kept.ObjectStore("s").Add("v", "k").Failed += (_, ev) => ev.Cancel();
            await Finished(kept);

            Assert.IsNull(kept.Error);
            Assert.AreEqual("first", await Get(db, "other"));
        }

        [Test]
        public async Task AnAbortedUpgradeLeavesTheDatabaseAsItWas()
        {
            var window = await OpenWindowAsync();
            var db = await OpenAsync(window, "db", 1, (d, _) => d.CreateObjectStore("old"));
            db.Close();

            var request = window.IndexedDbOfScript().Open("db", 2);
            request.UpgradeNeeded += (_, _) =>
            {
                ((IdbDatabase)request.Result).CreateObjectStore("new");
                ((IdbDatabase)request.Result).DeleteObjectStore("old");
                request.Transaction.Abort();
            };
            await Done(request);

            Assert.AreEqual((Int32)DomError.Abort, ((DomException)request.Error).Code);
            var again = await OpenAsync(window, "db", null, null);
            Assert.AreEqual(1, again.Version);
            Assert.AreEqual(new[] { "old" }, again.ObjectStoreNames.ToArray());
        }

        [Test]
        public async Task DeletingADatabaseAndOpeningItAgainStartsAnew()
        {
            var window = await OpenWindowAsync();
            var db = await OpenStoreAsync(window);
            await Done(db.Transaction("s", "readwrite").ObjectStore("s").Put("v", "k"));
            db.Close();

            var factory = window.IndexedDbOfScript();
            var delete = factory.DeleteDatabase("db");
            var upgrades = 0;
            var open = factory.Open("db");
            open.UpgradeNeeded += (_, _) => upgrades++;
            await Done(delete);
            await Done(open);

            Assert.AreEqual(1, upgrades);
            Assert.AreEqual(0, ((IdbDatabase)open.Result).ObjectStoreNames.Length);
        }

        [Test]
        public async Task CloseWaitsForTransactionsBeforeLettingAnUpgradeGoOn()
        {
            var window = await OpenWindowAsync();
            var db = await OpenStoreAsync(window);
            var tx = db.Transaction("s", "readwrite");
            var put = tx.ObjectStore("s").Put("v", "k");
            db.Close();
            Assert.Throws<DomException>(() => db.Transaction("s"));

            var upgrade = await OpenAsync(window, "db", 2, null);

            Assert.AreEqual("done", put.ReadyState);
            Assert.IsNull(put.Error);
            Assert.AreEqual(2, upgrade.Version);
        }

        [Test]
        public async Task KeysAreOrderedAsTheStandardSays()
        {
            var date = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var keys = new Object[] { new Object[] { 1.0 }, new Byte[] { 1 }, "a", date, 2.0, 1.0, "B", new Object[] { }, new Byte[] { } };

            var sorted = keys.OrderBy(m => m, Comparer<Object>.Create(IdbKeys.Compare)).ToArray();

            Assert.AreEqual(1.0, sorted[0]);
            Assert.AreEqual(2.0, sorted[1]);
            Assert.AreEqual(date, sorted[2]);
            Assert.AreEqual("B", sorted[3]);
            Assert.AreEqual("a", sorted[4]);
            Assert.AreEqual(0, ((Byte[])sorted[5]).Length);
            Assert.AreEqual(1, ((Byte[])sorted[6]).Length);
            Assert.AreEqual(0, ((Object[])sorted[7]).Length);
            Assert.AreEqual(1, ((Object[])sorted[8]).Length);
        }

        [TestCase(true)]
        [TestCase(Double.NaN)]
        public async Task InvalidKeysAreRejected(Object key)
        {
            var db = await OpenStoreAsync(await OpenWindowAsync());
            var store = db.Transaction("s", "readwrite").ObjectStore("s");

            var error = Assert.Throws<DomException>(() => store.Put("v", key));

            Assert.AreEqual("DataError", error.Name);
        }

        [Test]
        public async Task KeyPathGivesTheKeyAndAGeneratedOneIsPutThere()
        {
            var db = await OpenAsync(await OpenWindowAsync(), "db", 1, (d, _) =>
            {
                d.CreateObjectStore("path", new IdbObjectStoreParameters { KeyPath = "a.b" });
                d.CreateObjectStore("auto", new IdbObjectStoreParameters { KeyPath = "x.id", AutoIncrement = true });
                d.CreateObjectStore("plain", new IdbObjectStoreParameters { AutoIncrement = true });
            });

            var byPath = await In(db, m => m.Put(Record(("a", Record(("b", "k"))))), "path");
            var value = Record(("n", 1.0));
            var auto = await In(db, m => m.Put(value), "auto");
            var second = await In(db, m => m.Add("first"), "plain");
            var explicitKey = await In(db, m => m.Add("second", 10.0), "plain");
            var afterExplicit = await In(db, m => m.Add("third"), "plain");

            Assert.AreEqual("k", byPath.Result);
            Assert.AreEqual(1.0, auto.Result);
            Assert.AreEqual(1.0, ((IDictionary<String, Object>)value["x"])["id"]);
            Assert.AreEqual(1.0, second.Result);
            Assert.AreEqual(10.0, explicitKey.Result);
            Assert.AreEqual(11.0, afterExplicit.Result);
            var path = db.Transaction("path", "readwrite").ObjectStore("path");
            Assert.Throws<DomException>(() => path.Put(Record(("c", 1.0))));
            Assert.Throws<DomException>(() => path.Put(Record(("a", Record(("b", "k")))), "given"));
        }

        [Test]
        public async Task RangesSelectRecords()
        {
            var window = await OpenWindowAsync();
            var db = await OpenStoreAsync(window);
            var tx = db.Transaction("s", "readwrite");

            foreach (var i in new[] { 3.0, 1.0, 5.0, 2.0, 4.0 })
            {
                tx.ObjectStore("s").Put("v" + i, i);
            }

            await Finished(tx);

            var keys = (Object[])(await In(db, m => m.GetAllKeys())).Result;
            var bound = (Object[])(await In(db, m => m.GetAll(IdbKeyRange.Bound(window, 2.0, 4.0, true, false)))).Result;
            var upper = (await In(db, m => m.Count(IdbKeyRange.UpperBound(window, 3.0, true)))).Result;
            var lower = (Object[])(await In(db, m => m.GetAllKeys(IdbKeyRange.LowerBound(window, 4.0)))).Result;
            var limited = (Object[])(await In(db, m => m.GetAllKeys(null, 2))).Result;
            var first = (await In(db, m => m.GetKey(IdbKeyRange.LowerBound(window, 2.5)))).Result;
            await In(db, m => m.Delete(IdbKeyRange.Bound(window, 1.0, 2.0)));
            var left = (await In(db, m => m.Count())).Result;
            await In(db, m => m.Clear());
            var none = (await In(db, m => m.Count())).Result;

            Assert.AreEqual(new Object[] { 1.0, 2.0, 3.0, 4.0, 5.0 }, keys);
            Assert.AreEqual(new Object[] { "v3", "v4" }, bound);
            Assert.AreEqual(2.0, upper);
            Assert.AreEqual(new Object[] { 4.0, 5.0 }, lower);
            Assert.AreEqual(new Object[] { 1.0, 2.0 }, limited);
            Assert.AreEqual(3.0, first);
            Assert.AreEqual(3.0, left);
            Assert.AreEqual(0.0, none);
        }

        [Test]
        public async Task KeyRangeTellsWhatItIncludes()
        {
            var window = await OpenWindowAsync();

            Assert.IsTrue(IdbKeyRange.Only(window, 1.0).Includes(window, 1.0));
            Assert.IsFalse(IdbKeyRange.LowerBound(window, 1.0, true).Includes(window, 1.0));
            Assert.IsTrue(IdbKeyRange.UpperBound(window, "b").Includes(window, "a"));
            Assert.IsFalse(IdbKeyRange.Bound(window, 1.0, 2.0).Includes(window, "a"));
            Assert.Throws<DomException>(() => IdbKeyRange.Bound(window, 2.0, 1.0));
            Assert.Throws<DomException>(() => IdbKeyRange.Bound(window, 1.0, 1.0, true));
            Assert.Throws<DomException>(() => IdbKeyRange.Only(window, true));
        }

        private Task<IdbDatabase> OpenIndexedAsync(IWindow window) =>
            OpenAsync(window, "db", 1, (d, _) =>
            {
                var store = d.CreateObjectStore("s", new IdbObjectStoreParameters { KeyPath = "id" });
                store.CreateIndex("byName", "name");
                store.CreateIndex("byMail", "mail", new IdbIndexParameters { Unique = true });
                store.CreateIndex("byTag", "tags", new IdbIndexParameters { MultiEntry = true });
            });

        private static Dictionary<String, Object> Person(Double id, String name, String mail, params Object[] tags) =>
            Record(("id", id), ("name", name), ("mail", mail), ("tags", tags));

        [Test]
        public async Task IndexesFindRecords()
        {
            var db = await OpenIndexedAsync(await OpenWindowAsync());
            var tx = db.Transaction("s", "readwrite");
            tx.ObjectStore("s").Put(Person(1, "ann", "a@x", "red", "blue"));
            tx.ObjectStore("s").Put(Person(2, "bob", "b@x", "red"));
            tx.ObjectStore("s").Put(Person(3, "ann", "c@x"));
            await Finished(tx);

            var first = (IDictionary<String, Object>)(await In(db, m => m.Index("byName").Get("ann"))).Result;
            var key = (await In(db, m => m.Index("byName").GetKey("bob"))).Result;
            var anns = (Object[])(await In(db, m => m.Index("byName").GetAllKeys("ann"))).Result;
            var count = (await In(db, m => m.Index("byName").Count("ann"))).Result;
            var red = (Object[])(await In(db, m => m.Index("byTag").GetAllKeys("red"))).Result;
            await In(db, m => m.Delete(1.0));
            var redAfter = (Object[])(await In(db, m => m.Index("byTag").GetAllKeys("red"))).Result;
            await In(db, m => m.Put(Person(2, "bea", "b@x")));
            var bob = (await In(db, m => m.Index("byName").GetKey("bob"))).Result;

            Assert.AreEqual(1.0, first["id"]);
            Assert.AreEqual(2.0, key);
            Assert.AreEqual(new Object[] { 1.0, 3.0 }, anns);
            Assert.AreEqual(2.0, count);
            Assert.AreEqual(new Object[] { 1.0, 2.0 }, red);
            Assert.AreEqual(new Object[] { 2.0 }, redAfter);
            Assert.IsNull(bob);
        }

        [Test]
        public async Task UniqueIndexRefusesASecondRecord()
        {
            var db = await OpenIndexedAsync(await OpenWindowAsync());
            await In(db, m => m.Put(Person(1, "a", "same")));

            var replaced = await In(db, m => m.Put(Person(1, "b", "same")));
            Assert.IsNull(replaced.Error);

            var conflict = db.Transaction("s", "readwrite").ObjectStore("s").Put(Person(2, "c", "same"));
            conflict.Failed += (_, ev) => ev.Cancel();
            await Done(conflict);

            Assert.AreEqual("ConstraintError", ((DomException)conflict.Error).Name);
            Assert.AreEqual(1.0, (await In(db, m => m.Count())).Result);
        }

        [Test]
        public async Task AnIndexCreatedLaterCoversExistingRecords()
        {
            var window = await OpenWindowAsync();
            var db = await OpenAsync(window, "db", 1, (d, _) => d.CreateObjectStore("s", new IdbObjectStoreParameters { KeyPath = "id" }));
            await Finished(Fill(db));
            db.Close();

            IdbTransaction upgrade = null;
            var later = await OpenAsync(window, "db", 2, (d, t) =>
            {
                upgrade = t;
                t.ObjectStore("s").CreateIndex("byName", "name");
            });

            var keys = (await Done(later.Transaction("s").ObjectStore("s").Index("byName").GetAllKeys("ann"))).Result;

            Assert.AreEqual(new Object[] { 1.0 }, keys);
            Assert.AreEqual(new[] { "byName" }, later.Transaction("s").ObjectStore("s").IndexNames.ToArray());
        }

        private static IdbTransaction Fill(IdbDatabase db)
        {
            var tx = db.Transaction("s", "readwrite");
            tx.ObjectStore("s").Put(Person(1, "ann", "a"));
            tx.ObjectStore("s").Put(Person(2, "bob", "b"));
            return tx;
        }

        private async Task<List<String>> Walk(IdbRequest request, Func<IdbCursor, Boolean> step = null)
        {
            var seen = new List<String>();
            var completion = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
            request.Failed += (_, _) => completion.TrySetResult(false);
            request.Succeeded += (_, _) =>
            {
                if (request.Result is IdbCursor cursor)
                {
                    seen.Add($"{cursor.Key}/{cursor.PrimaryKey}");

                    if (step == null || step(cursor))
                    {
                        cursor.Continue();
                    }
                }
                else
                {
                    completion.TrySetResult(true);
                }
            };
            await Within(completion.Task);
            return seen;
        }

        private async Task<IdbObjectStore> OpenCursorDataAsync()
        {
            var db = await OpenIndexedAsync(await OpenWindowAsync());
            var tx = db.Transaction("s", "readwrite");
            var store = tx.ObjectStore("s");
            store.Put(Person(1, "a", "1"));
            store.Put(Person(2, "b", "2"));
            store.Put(Person(3, "b", "3"));
            store.Put(Person(4, "c", "4"));
            return store;
        }

        [TestCase(null, "1/1,2/2,3/3,4/4")]
        [TestCase("prev", "4/4,3/3,2/2,1/1")]
        public async Task CursorsWalkAStore(String direction, String expected)
        {
            var store = await OpenCursorDataAsync();

            var seen = await Walk(store.OpenCursor(null, direction));

            Assert.AreEqual(expected, String.Join(",", seen));
        }

        [TestCase(null, "a/1,b/2,b/3,c/4")]
        [TestCase("next", "a/1,b/2,b/3,c/4")]
        [TestCase("nextunique", "a/1,b/2,c/4")]
        [TestCase("prev", "c/4,b/3,b/2,a/1")]
        [TestCase("prevunique", "c/4,b/2,a/1")]
        public async Task CursorsWalkAnIndex(String direction, String expected)
        {
            var store = await OpenCursorDataAsync();

            var seen = await Walk(store.Index("byName").OpenKeyCursor(null, direction));

            Assert.AreEqual(expected, String.Join(",", seen));
        }

        [Test]
        public async Task CursorStaysInItsRange()
        {
            var store = await OpenCursorDataAsync();

            var seen = await Walk(store.OpenCursor(IdbKeyRange.Bound(store.Transaction.Window, 2.0, 3.0)));

            Assert.AreEqual("2/2,3/3", String.Join(",", seen));
        }

        [Test]
        public async Task CursorGivesTheValueOnlyIfItWasAskedFor()
        {
            var store = await OpenCursorDataAsync();
            var request = store.OpenCursor(2.0);
            await Done(request);

            Assert.IsInstanceOf<IdbCursorWithValue>(request.Result);
            Assert.AreEqual("b", ((IDictionary<String, Object>)((IdbCursorWithValue)request.Result).Value)["name"]);

            var keyOnly = await In(store.Transaction.Database, m => m.OpenKeyCursor());

            Assert.IsNotInstanceOf<IdbCursorWithValue>(keyOnly.Result);
        }

        [Test]
        public async Task AdvanceAndContinueSkipRecords()
        {
            var store = await OpenCursorDataAsync();
            var request = store.OpenCursor();
            var seen = new List<Object>();
            var completion = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
            request.Succeeded += (_, _) =>
            {
                if (request.Result is IdbCursor cursor)
                {
                    seen.Add(cursor.Key);

                    if (seen.Count == 1)
                    {
                        cursor.Advance(2);
                    }
                    else if (seen.Count == 2)
                    {
                        cursor.Continue(4.0);
                    }
                    else
                    {
                        cursor.Continue();
                    }
                }
                else
                {
                    completion.TrySetResult(true);
                }
            };

            await Within(completion.Task);

            Assert.AreEqual(new Object[] { 1.0, 3.0, 4.0 }, seen);
        }

        [Test]
        public async Task ContinueBackwardsIsRejected()
        {
            var store = await OpenCursorDataAsync();
            var request = store.OpenCursor(IdbKeyRange.LowerBound(store.Transaction.Window, 3.0));
            DomException error = null;
            var completion = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
            request.Succeeded += (_, _) =>
            {
                error = Assert.Throws<DomException>(() => ((IdbCursor)request.Result).Continue(1.0));
                completion.TrySetResult(true);
            };

            await Within(completion.Task);

            Assert.AreEqual("DataError", error.Name);
        }

        [Test]
        public async Task ContinuePrimaryKeyFindsTheRecord()
        {
            var store = await OpenCursorDataAsync();

            var skipped = false;
            var request = store.Index("byName").OpenCursor();
            var seen = await Walk(request, cursor =>
            {
                if (!skipped)
                {
                    skipped = true;
                    cursor.ContinuePrimaryKey("b", 3.0);
                    return false;
                }

                return true;
            });

            Assert.AreEqual("a/1,b/3,c/4", String.Join(",", seen));
        }

        [Test]
        public async Task CursorUpdatesAndDeletes()
        {
            var store = await OpenCursorDataAsync();
            var request = store.OpenCursor();
            var completion = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
            request.Succeeded += (_, _) =>
            {
                if (request.Result is IdbCursorWithValue cursor)
                {
                    if ((Double)cursor.Key == 1.0)
                    {
                        cursor.Update(Person(1, "new", "1"));
                    }
                    else if ((Double)cursor.Key == 2.0)
                    {
                        cursor.Delete();
                    }

                    cursor.Continue();
                }
                else
                {
                    completion.TrySetResult(true);
                }
            };

            await Within(completion.Task);

            var db = store.Transaction.Database;
            var keys = (Object[])(await In(db, m => m.GetAllKeys())).Result;
            var first = (IDictionary<String, Object>)(await In(db, m => m.Get(1.0))).Result;
            Assert.AreEqual(new Object[] { 1.0, 3.0, 4.0 }, keys);
            Assert.AreEqual("new", first["name"]);
        }

        [Test]
        public async Task ReadersShareAndAWriterWaitsItsTurn()
        {
            var db = await OpenStoreAsync(await OpenWindowAsync());
            var log = new List<String>();
            var firstWriter = db.Transaction("s", "readwrite");
            firstWriter.ObjectStore("s").Put("one", "k").Succeeded += (_, _) => { lock (log) { log.Add("write1"); } };
            var reader = db.Transaction("s");
            var read = reader.ObjectStore("s").Get("k");
            read.Succeeded += (_, _) => { lock (log) { log.Add("read:" + read.Result); } };
            var secondWriter = db.Transaction("s", "readwrite");
            secondWriter.ObjectStore("s").Put("two", "k").Succeeded += (_, _) => { lock (log) { log.Add("write2"); } };

            await Task.WhenAll(Finished(firstWriter), Finished(reader), Finished(secondWriter));

            Assert.AreEqual(new[] { "write1", "read:one", "write2" }, log);
        }

        [Test]
        public async Task TransactionsOfOtherStoresDoNotWait()
        {
            var db = await OpenAsync(await OpenWindowAsync(), "db", 1, (d, _) =>
            {
                d.CreateObjectStore("a");
                d.CreateObjectStore("b");
            });
            var hold = db.Transaction("a", "readwrite");
            var entered = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);
            var other = db.Transaction("b", "readwrite");
            other.ObjectStore("b").Put("v", "k").Succeeded += (_, _) => entered.TrySetResult(true);

            Assert.IsTrue(await Within(entered.Task));
            await Task.WhenAll(Finished(hold), Finished(other));
        }

        [Test]
        public async Task CommitEndsTheTransaction()
        {
            var db = await OpenStoreAsync(await OpenWindowAsync());
            var tx = db.Transaction("s", "readwrite");
            var store = tx.ObjectStore("s");
            store.Put("v", "k");

            tx.Commit();

            Assert.Throws<DomException>(() => store.Put("w", "l"));
            await Finished(tx);
            Assert.AreEqual("v", await Get(db, "k"));
        }

        [Test]
        public async Task CmpComparesKeys()
        {
            var factory = (await OpenWindowAsync()).IndexedDbOfScript();

            Assert.AreEqual(-1, factory.Cmp(1.0, 2.0));
            Assert.AreEqual(0, factory.Cmp("a", "a"));
            Assert.AreEqual(1, factory.Cmp("a", 2.0));
            Assert.Throws<DomException>(() => factory.Cmp(true, 1.0));
        }
    }
}
