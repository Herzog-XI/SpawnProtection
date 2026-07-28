using System;
using MEC;

namespace SpawnProtection
{
    internal sealed class ProtectionState
    {
        public DateTime SpawnedAt { get; set; }
        public DateTime FullProtectionEndsAt { get; set; }
        public DateTime TeamProtectionEndsAt { get; set; }
        public bool FullProtectionRemovedByAttack { get; set; }
        public CoroutineHandle TimerCoroutine { get; set; }
    }
}
