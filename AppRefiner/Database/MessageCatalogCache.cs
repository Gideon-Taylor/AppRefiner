using AppRefiner.Database.Models;
using System.Runtime.CompilerServices;

namespace AppRefiner.Database
{
    /// <summary>
    /// Memoizes Message Catalog queries per data manager. Keyed with a
    /// ConditionalWeakTable so cache state dies with the connection.
    /// Negative single-entry lookups are cached too, so hovering a typo'd
    /// MsgGet doesn't re-query on every mouse move. The catalog is nearly
    /// static; the dialog's Refresh button calls Clear().
    /// </summary>
    public static class MessageCatalogCache
    {
        private class State
        {
            public List<MessageSetInfo>? Sets;
            public readonly Dictionary<int, List<MessageCatalogEntry>> MessagesBySet = new();
            public readonly Dictionary<(int Set, int Num), MessageCatalogEntry?> SingleLookups = new();
        }

        private static readonly ConditionalWeakTable<IDataManager, State> States = new();

        public static List<MessageSetInfo> GetMessageSets(IDataManager dataManager)
        {
            var state = States.GetOrCreateValue(dataManager);
            lock (state)
            {
                state.Sets ??= dataManager.GetMessageSets();
                return state.Sets;
            }
        }

        public static List<MessageCatalogEntry> GetMessagesForSet(IDataManager dataManager, int setNumber)
        {
            var state = States.GetOrCreateValue(dataManager);
            lock (state)
            {
                if (!state.MessagesBySet.TryGetValue(setNumber, out var messages))
                {
                    messages = dataManager.GetMessagesForSet(setNumber);
                    state.MessagesBySet[setNumber] = messages;
                }
                return messages;
            }
        }

        public static MessageCatalogEntry? GetEntry(IDataManager dataManager, int setNumber, int messageNumber)
        {
            var state = States.GetOrCreateValue(dataManager);
            lock (state)
            {
                // A fully loaded set answers without another query
                if (state.MessagesBySet.TryGetValue(setNumber, out var loaded))
                {
                    return loaded.FirstOrDefault(m => m.MessageNumber == messageNumber);
                }

                if (!state.SingleLookups.TryGetValue((setNumber, messageNumber), out var entry))
                {
                    entry = dataManager.GetMessageCatalogEntry(setNumber, messageNumber);
                    state.SingleLookups[(setNumber, messageNumber)] = entry;
                }
                return entry;
            }
        }

        public static void Clear(IDataManager dataManager)
        {
            if (States.TryGetValue(dataManager, out var state))
            {
                lock (state)
                {
                    state.Sets = null;
                    state.MessagesBySet.Clear();
                    state.SingleLookups.Clear();
                }
            }
        }
    }
}
