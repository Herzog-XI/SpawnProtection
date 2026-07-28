using System;
using System.Collections.Generic;
using System.Threading;
using Exiled.API.Features;
using PlayerRoles;

namespace SpawnProtection
{
    internal sealed class ProtectionManager
    {
        private readonly Plugin plugin;
        private readonly Dictionary<Player, ProtectionState> states = new Dictionary<Player, ProtectionState>();
        private readonly object syncRoot = new object();

        public ProtectionManager(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public void Apply(Player player)
        {
            if (!IsEligible(player))
            {
                Remove(player);
                return;
            }

            Remove(player);

            DateTime now = DateTime.UtcNow;
            ProtectionState state = new ProtectionState
            {
                FullProtectionEndsAt = now.AddSeconds(Math.Max(0f, plugin.Config.FullProtectionDuration)),
                TeamProtectionEndsAt = now.AddSeconds(Math.Max(0f, plugin.Config.TeamProtectionDuration)),
                FullProtectionRemoved = plugin.Config.FullProtectionDuration <= 0f,
            };

            lock (syncRoot)
                states[player] = state;

            if (plugin.Config.ShowTimer)
            {
                int refreshMilliseconds = Math.Max(200, (int)(plugin.Config.TimerRefreshRate * 1000f));
                state.Timer = new Timer(_ => UpdateTimer(player), null, 0, refreshMilliseconds);
            }
        }

        public void Remove(Player player)
        {
            if (player == null)
                return;

            ProtectionState state;
            lock (syncRoot)
            {
                if (!states.TryGetValue(player, out state))
                    return;

                states.Remove(player);
            }

            state.Dispose();
        }

        public void RemoveFullProtection(Player player, bool showMessage)
        {
            if (player == null)
                return;

            ProtectionState state;
            lock (syncRoot)
            {
                if (!states.TryGetValue(player, out state) || !state.HasFullProtection)
                    return;

                state.FullProtectionRemoved = true;
            }

            if (showMessage && !string.IsNullOrWhiteSpace(plugin.Config.AttackEndedHint))
                player.ShowHint(plugin.Config.AttackEndedHint, plugin.Config.AttackEndedHintDuration);
        }

        public bool HasFullProtection(Player player)
        {
            if (player == null)
                return false;

            lock (syncRoot)
                return states.TryGetValue(player, out ProtectionState state) && state.HasFullProtection;
        }

        public bool HasTeamProtection(Player player)
        {
            if (player == null)
                return false;

            lock (syncRoot)
                return states.TryGetValue(player, out ProtectionState state) && state.HasTeamProtection;
        }

        public void ClearAll()
        {
            List<ProtectionState> activeStates;
            lock (syncRoot)
            {
                activeStates = new List<ProtectionState>(states.Values);
                states.Clear();
            }

            foreach (ProtectionState state in activeStates)
                state.Dispose();
        }

        public static bool AreFriendly(Player first, Player second)
        {
            if (first == null || second == null)
                return false;

            Team firstTeam = first.Role.Team;
            Team secondTeam = second.Role.Team;

            if (firstTeam == secondTeam)
                return true;

            return (firstTeam == Team.FoundationForces && secondTeam == Team.Scientists)
                || (firstTeam == Team.Scientists && secondTeam == Team.FoundationForces)
                || (firstTeam == Team.ChaosInsurgency && secondTeam == Team.ClassD)
                || (firstTeam == Team.ClassD && secondTeam == Team.ChaosInsurgency);
        }

        public static bool IsEligible(Player player)
        {
            if (player == null || !player.IsConnected)
                return false;

            RoleTypeId role = player.Role.Type;
            return player.Role.Team != Team.SCPs
                && role != RoleTypeId.Spectator
                && role != RoleTypeId.Overwatch
                && role != RoleTypeId.None;
        }

        private void UpdateTimer(Player player)
        {
            if (player == null || !player.IsConnected)
            {
                Remove(player);
                return;
            }

            ProtectionState state;
            lock (syncRoot)
            {
                if (!states.TryGetValue(player, out state))
                    return;
            }

            if (!state.HasTeamProtection)
            {
                Remove(player);
                return;
            }

            string text;
            if (state.HasFullProtection)
            {
                int seconds = Math.Max(0, (int)Math.Ceiling((state.FullProtectionEndsAt - DateTime.UtcNow).TotalSeconds));
                text = plugin.Config.FullProtectionHint?.Replace("{time}", seconds.ToString());
            }
            else
            {
                int seconds = Math.Max(0, (int)Math.Ceiling((state.TeamProtectionEndsAt - DateTime.UtcNow).TotalSeconds));
                text = plugin.Config.TeamProtectionHint?.Replace("{time}", seconds.ToString());
            }

            if (!string.IsNullOrWhiteSpace(text))
                player.ShowHint(text, Math.Max(0.35f, plugin.Config.TimerRefreshRate + 0.15f));
        }
    }
}
