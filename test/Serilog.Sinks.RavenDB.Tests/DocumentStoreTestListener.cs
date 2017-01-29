using System.Collections.Generic;
using Raven.Client.Listeners;
using Raven.Json.Linq;
using LogEvent = Serilog.Sinks.RavenDB.Data.LogEvent;

namespace Serilog.Sinks.RavenDB.Tests
{
    public class DocumentStoreTestListener : IDocumentStoreListener
    {
        public Dictionary<string, LogEvent> Events { get; set; } = new Dictionary<string, LogEvent>();

        public bool BeforeStore(string key, object entityInstance, RavenJObject metadata, RavenJObject original)
        {
            return true;
        }

        public void AfterStore(string key, object entityInstance, RavenJObject metadata)
        {
            Events.Add(key, (LogEvent) entityInstance);
        }
    }
}
