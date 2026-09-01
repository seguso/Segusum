using System.Collections.Concurrent;
using System.Threading;

namespace Seg
{
    /// <summary>
    /// Protegge le richieste API di un utente mentre viene creato un suo snapshot completo.
    /// Le richieste statiche del client non passano da questo gate.
    /// </summary>
    public static class ApiSerializationGate
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();

        public static SemaphoreSlim ForUser(string userName)
        {
            return Gates.GetOrAdd(userName, _ => new SemaphoreSlim(1, 1));
        }
    }
}
