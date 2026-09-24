using System;
using System.Collections.Generic;
using System.Threading;

namespace AshenWick.Art
{
    /// <summary>
    /// Thread-safe cache of painted canvases. Painting is pure managed code, so the expensive
    /// work (room terrain, parallax layers, characters) is done ahead of time on a worker thread;
    /// the main thread only uploads finished pixels to textures.
    /// </summary>
    public static class ArtCache
    {
        static readonly Dictionary<string, ArtCanvas> done = new Dictionary<string, ArtCanvas>();
        static readonly object gate = new object();
        static Thread worker;

        public static ArtCanvas Get(string key, Func<ArtCanvas> painter)
        {
            ArtCanvas c;
            lock (gate) { if (done.TryGetValue(key, out c)) return c; }
            c = painter();
            lock (gate) { done[key] = c; }
            return c;
        }

        public static bool Has(string key)
        {
            lock (gate) { return done.ContainsKey(key); }
        }

        /// <summary>Paints the given jobs in order on a background thread (no-op where threads are unavailable).</summary>
        public static void Prewarm(IList<KeyValuePair<string, Func<ArtCanvas>>> jobs)
        {
            if (worker != null) return;
            try
            {
                worker = new Thread(() =>
                {
                    foreach (var job in jobs)
                    {
                        try { Get(job.Key, job.Value); }
                        catch (Exception) { /* painted again on demand if something went wrong */ }
                    }
                });
                worker.IsBackground = true;
                worker.Priority = ThreadPriority.BelowNormal;
                worker.Start();
            }
            catch (Exception)
            {
                worker = null;
            }
        }
    }
}
