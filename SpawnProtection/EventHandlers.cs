using Exiled.Events.EventArgs.Player;

namespace SpawnProtection
{
    internal sealed class EventHandlers
    {
        private readonly ProtectionManager manager;

        public EventHandlers(ProtectionManager manager)
        {
            this.manager = manager;
        }

        public void OnSpawned(SpawnedEventArgs ev)
        {
            manager.Apply(ev.Player);
        }

        public void OnHurting(HurtingEventArgs ev)
        {
            if (ev.Player == null || !ev.IsAllowed)
                return;

            bool isPlayerAttack = ev.Attacker != null && ev.Attacker != ev.Player;

            // Full protection only blocks damage caused by another player.
            // Environmental damage such as falling, Tesla, Warhead or candies remains unaffected.
            if (isPlayerAttack && manager.HasFullProtection(ev.Player))
            {
                ev.IsAllowed = false;
                return;
            }

            if (isPlayerAttack
                && manager.HasTeamProtection(ev.Player)
                && ProtectionManager.AreFriendly(ev.Attacker, ev.Player))
            {
                ev.IsAllowed = false;
                return;
            }

            if (Plugin.Instance.Config.RemoveFullProtectionOnAttack
                && isPlayerAttack
                && ev.Amount > 0f
                && manager.HasFullProtection(ev.Attacker))
            {
                manager.RemoveFullProtection(ev.Attacker, true);
            }
        }

        public void OnDied(DiedEventArgs ev)
        {
            manager.Remove(ev.Player);
        }

        public void OnChangingRole(ChangingRoleEventArgs ev)
        {
            manager.Remove(ev.Player);
        }

        public void OnRestartingRound()
        {
            manager.ClearAll();
        }
    }
}
