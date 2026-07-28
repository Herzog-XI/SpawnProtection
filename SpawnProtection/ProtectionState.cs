using System;
using MEC;

namespace SpawnProtection
{
    internal sealed class ProtectionState
    {
        public DateTime FullProtectionEndsAt { get; set; }
        public DateTime TeamProtectionEndsAt { get; set; }
        public bool FullProtectionRemoved { get; set; }
        public CoroutineHandle TimerCoroutine { get; set; }

        public bool HasFullProtection => !FullProtectionRemoved && DateTime.UtcNow < FullProtectionEndsAt;
        public bool HasTeamProtection => DateTime.UtcNow < TeamProtectionEndsAt;
    }
}
