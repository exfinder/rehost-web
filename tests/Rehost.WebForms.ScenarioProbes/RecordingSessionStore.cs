using System.Collections.Specialized;
using System.Web;
using System.Web.SessionState;
using Rehost.WebForms.ScenarioProtocol;

namespace Rehost.WebForms.ScenarioProbes;

// A working in-memory store that also records the call sequence the module drives it with.
// Round-tripping values proves nothing about the locking protocol on its own: a store that
// ignored exclusivity entirely would round-trip identically, so the recorded sequence is the
// claim and the stored values are only what keeps the application running while it is made.
public sealed class RecordingSessionStore : SessionStateStoreProviderBase
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, Entry> Items = [];

    private sealed class Entry
    {
        internal SessionStateStoreData? Data { get; set; }

        internal object? LockId { get; set; }

        internal DateTime LockedAtUtc { get; set; }

        internal SessionStateActions Actions { get; set; }
    }

    public override void Initialize(string name, NameValueCollection config) =>
        base.Initialize(string.IsNullOrEmpty(name) ? nameof(RecordingSessionStore) : name, config);

    public override void Dispose()
    {
    }

    public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback) => false;

    public override void InitializeRequest(HttpContext context)
    {
    }

    public override void EndRequest(HttpContext context)
    {
    }

    public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout) =>
        new(new SessionStateItemCollection(), new HttpStaticObjectsCollection(), timeout);

    public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
    {
        Record("CreateUninitializedItem");
        lock (Gate)
        {
            Items[id] = new Entry
            {
                Data = CreateNewStoreData(context, timeout),
                Actions = SessionStateActions.InitializeItem,
            };
        }
    }

    public override SessionStateStoreData? GetItem(
        HttpContext context,
        string id,
        out bool locked,
        out TimeSpan lockAge,
        out object? lockId,
        out SessionStateActions actions)
    {
        Record("GetItem");
        return Read(id, exclusive: false, out locked, out lockAge, out lockId, out actions);
    }

    public override SessionStateStoreData? GetItemExclusive(
        HttpContext context,
        string id,
        out bool locked,
        out TimeSpan lockAge,
        out object? lockId,
        out SessionStateActions actions)
    {
        Record("GetItemExclusive");
        return Read(id, exclusive: true, out locked, out lockAge, out lockId, out actions);
    }

    public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
    {
        Record("ReleaseItemExclusive");
        lock (Gate)
        {
            if (Items.TryGetValue(id, out var entry) && Equals(entry.LockId, lockId))
            {
                entry.LockId = null;
            }
        }
    }

    public override void SetAndReleaseItemExclusive(
        HttpContext context,
        string id,
        SessionStateStoreData item,
        object lockId,
        bool newItem)
    {
        Record("SetAndReleaseItemExclusive");
        lock (Gate)
        {
            if (!newItem && Items.TryGetValue(id, out var existing) && !Equals(existing.LockId, lockId))
            {
                return;
            }

            Items[id] = new Entry { Data = item, Actions = SessionStateActions.None };
        }
    }

    public override void RemoveItem(
        HttpContext context,
        string id,
        object lockId,
        SessionStateStoreData item)
    {
        Record("RemoveItem");
        lock (Gate)
        {
            Items.Remove(id);
        }
    }

    public override void ResetItemTimeout(HttpContext context, string id) =>
        Record("ResetItemTimeout");

    private static SessionStateStoreData? Read(
        string id,
        bool exclusive,
        out bool locked,
        out TimeSpan lockAge,
        out object? lockId,
        out SessionStateActions actions)
    {
        lock (Gate)
        {
            locked = false;
            lockAge = TimeSpan.Zero;
            lockId = null;
            actions = SessionStateActions.None;

            if (!Items.TryGetValue(id, out var entry) || entry.Data == null)
            {
                return null;
            }

            if (entry.LockId != null)
            {
                locked = true;
                lockAge = DateTime.UtcNow - entry.LockedAtUtc;
                lockId = entry.LockId;
                return null;
            }

            if (exclusive)
            {
                entry.LockId = Guid.NewGuid();
                entry.LockedAtUtc = DateTime.UtcNow;
                lockId = entry.LockId;
                actions = entry.Actions;
                entry.Actions = SessionStateActions.None;
            }

            return entry.Data;
        }
    }

    private static void Record(string call) =>
        Witness.Record(WitnessProtocol.SessionStoreCall + call);
}
