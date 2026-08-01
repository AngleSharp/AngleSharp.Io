namespace AngleSharp.Io.Tests.IndexedDb
{
    using AngleSharp;
    using AngleSharp.Dom;
    using AngleSharp.Io.Dom;
    using AngleSharp.Io.IndexedDb;
    using NUnit.Framework;
    using System.Collections.Generic;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    [TestFixture]
    public class IndexedDbTests
    {
        [Test]
        public async Task ConfigurationCanRegisterIndexedDb()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");

            Assert.IsNotNull(window.IndexedDb());
        }

        [Test]
        public async Task IndexedDbSharesStateAcrossSameOriginWindows()
        {
            var config = CreateConfigWithIndexedDb();
            var contextA = BrowsingContext.New(config);
            var contextB = BrowsingContext.New(config);

            var windowA = await OpenWindowAsync(contextA, "https://indexeddb.example/");
            var windowB = await OpenWindowAsync(contextB, "https://indexeddb.example/other");

            var storeA = windowA.IndexedDb().Open("app").CreateObjectStore("records");
            var storeB = windowB.IndexedDb().Open("app").CreateObjectStore("records");

            storeA["token"] = "abc";

            Assert.AreEqual("abc", storeB["token"]);
        }

        [Test]
        public async Task IndexedDbIsOriginBound()
        {
            var config = CreateConfigWithIndexedDb();
            var contextA = BrowsingContext.New(config);
            var contextB = BrowsingContext.New(config);

            var windowA = await OpenWindowAsync(contextA, "https://indexeddb.example/");
            var windowB = await OpenWindowAsync(contextB, "https://other.example/");

            var storeA = windowA.IndexedDb().Open("app").CreateObjectStore("records");
            var storeB = windowB.IndexedDb().Open("app").CreateObjectStore("records");

            storeA["token"] = "abc";

            Assert.AreEqual("abc", storeA["token"]);
            Assert.AreEqual(null, storeB["token"]);
        }

        [Test]
        public async Task OpenWithVersionRunsUpgradeAndSetsVersion()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var factory = window.IndexedDb();

            var db = factory.Open("app", 3, tx =>
            {
                var store = tx.CreateObjectStore("records");
                store["hello"] = "world";
            });

            Assert.AreEqual(3, db.Version);
            Assert.AreEqual("world", db.CreateObjectStore("records")["hello"]);
        }

        [Test]
        public async Task OpenWithLowerVersionThrows()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var factory = window.IndexedDb();

            factory.Open("app", 2, tx => tx.CreateObjectStore("records"));

            Assert.Throws<InvalidOperationException>(() => factory.Open("app", 1));
        }

        [Test]
        public async Task ReadWriteTransactionCommitPersistsChanges()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var db = window.IndexedDb().Open("app", 1, tx => tx.CreateObjectStore("records"));

            using (var transaction = db.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite))
            {
                var store = transaction.GetObjectStore("records");
                store["token"] = "abc";
                transaction.Commit();
            }

            Assert.AreEqual("abc", db.CreateObjectStore("records")["token"]);
        }

        [Test]
        public async Task ReadWriteTransactionAbortDiscardsChanges()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var db = window.IndexedDb().Open("app", 1, tx => tx.CreateObjectStore("records"));

            using (var transaction = db.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite))
            {
                var store = transaction.GetObjectStore("records");
                store["token"] = "abc";
                transaction.Abort();
            }

            Assert.AreEqual(null, db.CreateObjectStore("records")["token"]);
        }

        [Test]
        public async Task ReadOnlyTransactionRejectsWrites()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var db = window.IndexedDb().Open("app", 1, tx =>
            {
                var store = tx.CreateObjectStore("records");
                store["token"] = "abc";
            });

            using (var transaction = db.BeginTransaction("records", IndexedDbTransactionMode.ReadOnly))
            {
                var store = transaction.GetObjectStore("records");
                Assert.Throws<InvalidOperationException>(() => store["token"] = "updated");
                transaction.Commit();
            }

            Assert.AreEqual("abc", db.CreateObjectStore("records")["token"]);
        }

        [Test]
        public async Task OpenRequestCompletesWithDatabase()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");

            var request = window.IndexedDb().OpenRequest("app", 1, tx => tx.CreateObjectStore("records"));
            var db = await request.WaitAsync();

            Assert.IsTrue(request.IsCompleted);
            Assert.AreEqual(false, request.HasError);
            Assert.IsNotNull(db);
            Assert.AreEqual(1, db.Version);
        }

        [Test]
        public async Task OpenRequestCapturesVersionError()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var factory = window.IndexedDb();

            factory.Open("app", 2, tx => tx.CreateObjectStore("records"));
            var request = factory.OpenRequest("app", 1);

            Assert.ThrowsAsync(Is.InstanceOf<InvalidOperationException>(), async () => await request.WaitAsync());
            Assert.IsTrue(request.IsCompleted);
            Assert.IsTrue(request.HasError);
            Assert.IsNotNull(request.Exception);
        }

        [Test]
        public async Task ObjectStoreAddPutDeleteCountBehavesAsExpected()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var db = window.IndexedDb().Open("app", 1, tx => tx.CreateObjectStore("records"));

            using (var transaction = db.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite))
            {
                var store = transaction.GetObjectStore("records");
                store.Add("k1", "v1");
                store.Put("k2", "v2");
                store.Put("k2", "v2b");

                Assert.AreEqual(2, store.Count());
                Assert.AreEqual("v1", store.Get("k1"));
                Assert.AreEqual("v2b", store.Get("k2"));
                Assert.AreEqual(true, store.Delete("k1"));
                Assert.AreEqual(false, store.Delete("missing"));
                Assert.AreEqual(1, store.Count());

                transaction.Commit();
            }

            using (var transaction = db.BeginTransaction("records", IndexedDbTransactionMode.ReadOnly))
            {
                var store = transaction.GetObjectStore("records");
                Assert.AreEqual(1, store.Count());
                Assert.AreEqual(null, store.Get("k1"));
                Assert.AreEqual("v2b", store.Get("k2"));
                transaction.Commit();
            }
        }

        [Test]
        public async Task ObjectStoreCanQueryWithKeyRanges()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var db = window.IndexedDb().Open("app", 1, tx => tx.CreateObjectStore("records"));

            using (var transaction = db.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite))
            {
                var store = transaction.GetObjectStore("records");
                store.Put("a", "id=1;type=note");
                store.Put("b", "id=2;type=task");
                store.Put("c", "id=3;type=note");
                store.Put("d", "id=4;type=note");
                transaction.Commit();
            }

            using (var transaction = db.BeginTransaction("records", IndexedDbTransactionMode.ReadOnly))
            {
                var store = transaction.GetObjectStore("records");
                var all = store.GetAll(IndexedDbKeyRange.Bound("b", "d"));
                var openUpper = store.GetAll(IndexedDbKeyRange.Bound("b", "d", false, true));

                CollectionAssert.AreEqual(new[] { "id=2;type=task", "id=3;type=note", "id=4;type=note" }, all.ToArray());
                CollectionAssert.AreEqual(new[] { "id=2;type=task", "id=3;type=note" }, openUpper.ToArray());
                Assert.AreEqual(2, store.Count(IndexedDbKeyRange.Bound("b", "d", false, true)));
                transaction.Commit();
            }
        }

        [Test]
        public async Task ObjectStoreIndexesCanQueryAndCount()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var db = window.IndexedDb().Open("app", 1, tx => tx.CreateObjectStore("records"));

            using (var transaction = db.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite))
            {
                var store = transaction.GetObjectStore("records");
                var index = store.CreateIndex("byType", "type");

                store.Put("a", "id=1;type=note");
                store.Put("b", "id=2;type=task");
                store.Put("c", "id=3;type=note");

                Assert.AreEqual("id=1;type=note", index.Get("note"));
                CollectionAssert.AreEqual(new[] { "id=1;type=note", "id=3;type=note" }, index.GetAll("note").ToArray());
                Assert.AreEqual(2, index.Count("note"));
                CollectionAssert.AreEquivalent(new[] { "id=1;type=note", "id=3;type=note", "id=2;type=task" }, index.GetAll(IndexedDbKeyRange.Bound("note", "task")).ToArray());

                transaction.Commit();
            }
        }

        [Test]
        public async Task UniqueIndexRejectsDuplicateProjectedValues()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var db = window.IndexedDb().Open("app", 1, tx => tx.CreateObjectStore("records"));

            using (var transaction = db.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite))
            {
                var store = transaction.GetObjectStore("records");
                store.CreateIndex("byEmail", "email", unique: true);
                store.Put("a", "email=alice@example.com;type=user");

                Assert.Throws<InvalidOperationException>(() => store.Put("b", "email=alice@example.com;type=user"));

                transaction.Commit();
            }
        }

        [Test]
        public async Task ObjectStoreCursorIteratesByKeyDirectionAndRange()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var db = window.IndexedDb().Open("app", 1, tx => tx.CreateObjectStore("records"));

            using (var transaction = db.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite))
            {
                var store = transaction.GetObjectStore("records");
                store.Put("a", "v1");
                store.Put("b", "v2");
                store.Put("c", "v3");
                store.Put("d", "v4");
                transaction.Commit();
            }

            using (var transaction = db.BeginTransaction("records", IndexedDbTransactionMode.ReadOnly))
            {
                var store = transaction.GetObjectStore("records");

                var next = ReadCursorValues(store.OpenCursor(IndexedDbKeyRange.Bound("b", "d")));
                CollectionAssert.AreEqual(new[] { "v2", "v3", "v4" }, next);

                var prev = ReadCursorValues(store.OpenCursor(IndexedDbKeyRange.Bound("b", "d"), IndexedDbCursorDirection.Prev));
                CollectionAssert.AreEqual(new[] { "v4", "v3", "v2" }, prev);

                transaction.Commit();
            }
        }

        [Test]
        public async Task IndexCursorIteratesByIndexKeyDirectionAndRange()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var db = window.IndexedDb().Open("app", 1, tx => tx.CreateObjectStore("records"));

            using (var transaction = db.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite))
            {
                var store = transaction.GetObjectStore("records");
                var index = store.CreateIndex("byType", "type");

                store.Put("a", "id=1;type=note");
                store.Put("b", "id=2;type=task");
                store.Put("c", "id=3;type=memo");
                store.Put("d", "id=4;type=task");

                var next = ReadCursorValues(index.OpenCursor(IndexedDbKeyRange.Bound("memo", "task")));
                CollectionAssert.AreEqual(new[] { "id=3;type=memo", "id=1;type=note", "id=2;type=task", "id=4;type=task" }, next);

                var prev = ReadCursorValues(index.OpenCursor(IndexedDbKeyRange.Bound("memo", "task"), IndexedDbCursorDirection.Prev));
                CollectionAssert.AreEqual(new[] { "id=4;type=task", "id=2;type=task", "id=1;type=note", "id=3;type=memo" }, prev);

                transaction.Commit();
            }
        }

        [Test]
        public async Task OpenRequestReportsBlockedWhenUpgradeHasOpenConnections()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var factory = window.IndexedDb();

            var db = factory.Open("app", 1, tx => tx.CreateObjectStore("records"));
            var request = factory.OpenRequest("app", 2, tx => tx.CreateObjectStore("events"));

            Assert.AreEqual(IndexedDbRequestState.Blocked, request.State);
            Assert.AreEqual(true, request.HasError);
            Assert.IsNotNull(request.Exception);
            Assert.ThrowsAsync(Is.InstanceOf<InvalidOperationException>(), async () => await request.WaitAsync());

            db.Close();

            var retry = factory.OpenRequest("app", 2, tx => tx.CreateObjectStore("events"));
            var upgraded = await retry.WaitAsync();

            Assert.AreEqual(IndexedDbRequestState.Success, retry.State);
            Assert.AreEqual(2, upgraded.Version);
        }

        [Test]
        public async Task OpenRequestCanWaitForUnblockAndResolveAutomatically()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var factory = window.IndexedDb();

            var db = factory.Open("app", 1, tx => tx.CreateObjectStore("records"));
            var request = factory.OpenRequest("app", 2, tx => tx.CreateObjectStore("events"), waitForUnblock: true);

            Assert.AreEqual(IndexedDbRequestState.Blocked, request.State);
            Assert.AreEqual(false, request.IsCompleted);

            db.Close();

            var upgraded = await request.WaitAsync();

            Assert.AreEqual(IndexedDbRequestState.Success, request.State);
            Assert.AreEqual(2, upgraded.Version);
        }

        [Test]
        public async Task OpenConnectionReceivesVersionChangeNotificationWhenUpgradeIsBlocked()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var factory = window.IndexedDb();
            var db = factory.Open("app", 1, tx => tx.CreateObjectStore("records"));

            var notifications = 0;
            var oldVersion = 0L;
            var newVersion = 0L;

            db.VersionChangeRequested += (oldV, newV) =>
            {
                notifications++;
                oldVersion = oldV;
                newVersion = newV;
            };

            var blocked = factory.OpenRequest("app", 2, tx => tx.CreateObjectStore("events"));

            Assert.AreEqual(IndexedDbRequestState.Blocked, blocked.State);
            Assert.AreEqual(1, notifications);
            Assert.AreEqual(1, oldVersion);
            Assert.AreEqual(2, newVersion);

            db.Close();
        }

        [Test]
        public async Task ClosingDatabasePreventsFurtherUsage()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var db = window.IndexedDb().Open("app", 1, tx => tx.CreateObjectStore("records"));

            db.Close();

            Assert.AreEqual(true, db.IsClosed);
            Assert.Throws<InvalidOperationException>(() => db.CreateObjectStore("other"));
            Assert.Throws<InvalidOperationException>(() => db.BeginTransaction("records", IndexedDbTransactionMode.ReadOnly));
        }

        [Test]
        public async Task DeleteDatabaseRequestReportsBlockedWhenConnectionsAreOpen()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var factory = window.IndexedDb();
            var db = factory.Open("app", 1, tx => tx.CreateObjectStore("records"));

            var blocked = factory.DeleteDatabaseRequest("app");

            Assert.AreEqual(IndexedDbRequestState.Blocked, blocked.State);
            Assert.AreEqual(true, blocked.HasError);
            Assert.ThrowsAsync(Is.InstanceOf<InvalidOperationException>(), async () => await blocked.WaitAsync());

            db.Close();

            var deleted = await factory.DeleteDatabaseRequest("app").WaitAsync();
            Assert.AreEqual(true, deleted);
        }

        [Test]
        public async Task DeleteDatabaseRequestCanWaitForUnblockAndResolveAutomatically()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var factory = window.IndexedDb();
            var db = factory.Open("app", 1, tx => tx.CreateObjectStore("records"));

            var request = factory.DeleteDatabaseRequest("app", waitForUnblock: true);

            Assert.AreEqual(IndexedDbRequestState.Blocked, request.State);
            Assert.AreEqual(false, request.IsCompleted);

            db.Close();

            var deleted = await request.WaitAsync();
            Assert.AreEqual(true, deleted);
            Assert.AreEqual(IndexedDbRequestState.Success, request.State);
        }

        [Test]
        public async Task TransactionLifecycleFlagsAndEventsAreReported()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var db = window.IndexedDb().Open("app", 1, tx => tx.CreateObjectStore("records"));

            var completedRaised = false;

            using (var transaction = db.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite))
            {
                transaction.Completed += () => completedRaised = true;
                var store = transaction.GetObjectStore("records");
                store.Put("k1", "v1");
                transaction.Commit();

                Assert.AreEqual(true, transaction.IsCompleted);
                Assert.AreEqual(false, transaction.IsAborted);
                Assert.AreEqual(true, completedRaised);
            }

            var abortedRaised = false;

            using (var transaction = db.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite))
            {
                transaction.Aborted += () => abortedRaised = true;
                transaction.Abort();

                Assert.AreEqual(true, transaction.IsCompleted);
                Assert.AreEqual(true, transaction.IsAborted);
                Assert.AreEqual(true, abortedRaised);
            }
        }

        [Test]
        public async Task OnlyOneReadWriteTransactionCanBeActiveAtATime()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var db = window.IndexedDb().Open("app", 1, tx => tx.CreateObjectStore("records"));

            var first = db.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite);

            Assert.Throws<InvalidOperationException>(() => db.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite));

            first.Abort();

            using (var second = db.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite))
            {
                var store = second.GetObjectStore("records");
                store.Put("k2", "v2");
                second.Commit();
            }

            Assert.AreEqual("v2", db.CreateObjectStore("records")["k2"]);
        }

        [Test]
        public async Task OpenRequestSuccessCallbackIsInvoked()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");

            var callbackCount = 0;
            IIndexedDbDatabase callbackDb = null;

            var request = window.IndexedDb().OpenRequest("app", 1, tx => tx.CreateObjectStore("records"));
            request.Succeeded += db =>
            {
                callbackCount++;
                callbackDb = db;
            };

            var opened = await request.WaitAsync();

            Assert.AreEqual(IndexedDbRequestState.Success, request.State);
            Assert.AreEqual(1, callbackCount);
            Assert.AreSame(opened, callbackDb);
        }

        [Test]
        public async Task OpenRequestWaitForUnblockRaisesBlockedThenSucceeded()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var factory = window.IndexedDb();
            var hold = factory.Open("app", 1, tx => tx.CreateObjectStore("records"));

            var blockedCount = 0;
            var failedCount = 0;
            var successCount = 0;

            var request = factory.OpenRequest("app", 2, tx => tx.CreateObjectStore("events"), waitForUnblock: true);
            request.Blocked += () => blockedCount++;
            request.Failed += _ => failedCount++;
            request.Succeeded += _ => successCount++;

            Assert.AreEqual(IndexedDbRequestState.Blocked, request.State);
            Assert.AreEqual(1, blockedCount);
            Assert.AreEqual(1, failedCount);
            Assert.AreEqual(0, successCount);

            hold.Close();

            var opened = await request.WaitAsync();

            Assert.IsNotNull(opened);
            Assert.AreEqual(IndexedDbRequestState.Success, request.State);
            Assert.AreEqual(1, blockedCount);
            Assert.AreEqual(1, failedCount);
            Assert.AreEqual(1, successCount);
        }

        [Test]
        public async Task DeleteRequestWaitForUnblockRaisesBlockedThenSucceeded()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var factory = window.IndexedDb();
            var hold = factory.Open("app", 1, tx => tx.CreateObjectStore("records"));

            var blockedCount = 0;
            var failedCount = 0;
            var successCount = 0;

            var request = factory.DeleteDatabaseRequest("app", waitForUnblock: true);
            request.Blocked += () => blockedCount++;
            request.Failed += _ => failedCount++;
            request.Succeeded += _ => successCount++;

            Assert.AreEqual(IndexedDbRequestState.Blocked, request.State);
            Assert.AreEqual(1, blockedCount);
            Assert.AreEqual(1, failedCount);
            Assert.AreEqual(0, successCount);

            hold.Close();

            var deleted = await request.WaitAsync();

            Assert.AreEqual(true, deleted);
            Assert.AreEqual(IndexedDbRequestState.Success, request.State);
            Assert.AreEqual(1, blockedCount);
            Assert.AreEqual(1, failedCount);
            Assert.AreEqual(1, successCount);
        }

        [Test]
        public async Task DisposingUncommittedReadWriteTransactionReleasesWriteLock()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var db = window.IndexedDb().Open("app", 1, tx => tx.CreateObjectStore("records"));

            IIndexedDbTransaction abandoned;

            using (abandoned = db.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite))
            {
                abandoned.GetObjectStore("records").Put("k1", "v1");
            }

            Assert.AreEqual(true, abandoned.IsCompleted);
            Assert.AreEqual(true, abandoned.IsAborted);

            using (var next = db.BeginTransaction("records", IndexedDbTransactionMode.ReadWrite))
            {
                next.GetObjectStore("records").Put("k2", "v2");
                next.Commit();
            }

            Assert.AreEqual(null, db.CreateObjectStore("records")["k1"]);
            Assert.AreEqual("v2", db.CreateObjectStore("records")["k2"]);
        }

        [Test]
        public async Task ClosedConnectionDoesNotReceiveVersionChangeNotification()
        {
            var config = CreateConfigWithIndexedDb();
            var context = BrowsingContext.New(config);
            var window = await OpenWindowAsync(context, "https://indexeddb.example/");
            var factory = window.IndexedDb();

            var closedConnection = factory.Open("app", 1, tx => tx.CreateObjectStore("records"));
            var activeConnection = factory.Open("app", 1);

            var closedNotifications = 0;
            var activeNotifications = 0;

            closedConnection.VersionChangeRequested += (_, _) => closedNotifications++;
            activeConnection.VersionChangeRequested += (_, _) => activeNotifications++;

            closedConnection.Close();

            var blocked = factory.OpenRequest("app", 2, tx => tx.CreateObjectStore("events"));

            Assert.AreEqual(IndexedDbRequestState.Blocked, blocked.State);
            Assert.AreEqual(0, closedNotifications);
            Assert.AreEqual(1, activeNotifications);

            activeConnection.Close();
        }

        private static List<String> ReadCursorValues(IIndexedDbCursor cursor)
        {
            var values = new List<String>();

            while (cursor != null)
            {
                values.Add(cursor.Value);
                cursor = cursor.Continue();
            }

            return values;
        }

        private static IConfiguration CreateConfigWithIndexedDb()
        {
            var factory = new IndexedDbProviderFactory();
            return Configuration.Default.WithIndexedDbProviderFactory(factory);
        }

        private static async Task<IWindow> OpenWindowAsync(IBrowsingContext context, String address)
        {
            var document = await context.OpenAsync(res =>
                res.Address(address).Content("<!doctype html><title>indexeddb</title>"));
            return document.DefaultView;
        }
    }
}