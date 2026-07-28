using System;
using System.Threading;

namespace SpawnProtection
{
    internal sealed class ProtectionState : IDisposable
    {
        public DateTime FullProtectionEndsAt { get; set; }
        public DateTime TeamProtectionEndsAt { get; set; }
        public bool FullProtectionRemoved { get; set; }
        public Timer Timer { get; set; }

        public bool HasFullProtection => !FullProtectionRemoved && DateTime.UtcNow < FullProtectionEndsAt;
        public bool HasTeamProtection => DateTime.UtcNow < TeamProtectionEndsAt;

        public void Dispose()
        {
            Timer?.Dispose();
            Timer = null;
        }
    }
}
