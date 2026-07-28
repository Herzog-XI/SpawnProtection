using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using MEC;

namespace SpawnProtection
{
    internal sealed class EventHandlers
    {
        public void OnVerified(VerifiedEventArgs ev)
        {
            Timing.CallDelayed(0.5f, () =>
            {
                if (ev.Player != null && ProtectionManager.IsEligible(ev.Player))
                    ProtectionManager.Apply(ev.Player);
            });
        }

        public void OnChangingRole(ChangingRoleEventArgs ev)
        {
            ProtectionManager.Remove(ev.Player);

            Timing.CallDelayed(0.6f, () =>
            {
                if (ev.Player != null && ProtectionManager.IsEligible(ev.Player))
                    ProtectionManager.Apply(ev.Player);
            });
        }

        public void OnHurting(HurtingEventArgs ev)
        {
            if (ev.Player == null || !ev.IsAllowed)
                return;

            if (ProtectionManager.HasFullProtection(ev.Player))
            {
                ev.IsAllowed = false;
                ev.Amount = 0f;
                return;
            }

            Player attacker = ev.Attacker;
            if (attacker == null || attacker == ev.Player)
                return;

            if (Plugin.Instance.Config.RemoveFullProtectionOnAttack && ev.Amount > 0f)
                ProtectionManager.RemoveFullProtectionBecauseOfAttack(attacker);

            if (ProtectionManager.HasTeamProtection(ev.Player) && attacker.Role.Team == ev.Player.Role.Team)
            {
                ev.IsAllowed = false;
                ev.Amount = 0f;
            }
        }

        public void OnDied(DiedEventArgs ev)
        {
            ProtectionManager.Remove(ev.Player);
        }

        public void OnDestroying(DestroyingEventArgs ev)
        {
            ProtectionManager.Remove(ev.Player);
        }

        public void OnRoundEnded(RoundEndedEventArgs ev)
        {
            ProtectionManager.ClearAll();
        }

        public void OnRestartingRound()
        {
            ProtectionManager.ClearAll();
        }
    }
}
