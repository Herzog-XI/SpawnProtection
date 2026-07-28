using System;
using System.Collections.Generic;
using Exiled.API.Features;
using MEC;
using PlayerRoles;

namespace SpawnProtection
{
    internal sealed class ProtectionManager
    {
        private readonly Plugin plugin;
        private readonly Dictionary<Player, ProtectionState> states = new Dictionary<Player, ProtectionState>();

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

            states[player] = state;

            if (plugin.Config.ShowTimer)
                state.TimerCoroutine = Timing.RunCoroutine(Timer(player));
        }

        public void Remove(Player player)
        {
            if (player == null || !states.TryGetValue(player, out ProtectionState state))
                return;

            if (state.TimerCoroutine.IsRunning)
                Timing.KillCoroutines(state.TimerCoroutine);

            states.Remove(player);
        }

        public void RemoveFullProtection(Player player, bool showMessage)
        {
            if (player == null || !states.TryGetValue(player, out ProtectionState state) || !state.HasFullProtection)
                return;

            state.FullProtectionRemoved = true;

            if (showMessage && !string.IsNullOrWhiteSpace(plugin.Config.AttackEndedHint))
                player.ShowHint(plugin.Config.AttackEndedHint, plugin.Config.AttackEndedHintDuration);
        }

        public bool HasFullProtection(Player player)
        {
            return player != null && states.TryGetValue(player, out ProtectionState state) && state.HasFullProtection;
        }

        public bool HasTeamProtection(Player player)
        {
            return player != null && states.TryGetValue(player, out ProtectionState state) && state.HasTeamProtection;
        }

        public void ClearAll()
        {
            foreach (ProtectionState state in states.Values)
            {
                if (state.TimerCoroutine.IsRunning)
                    Timing.KillCoroutines(state.TimerCoroutine);
            }

            states.Clear();
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

        private IEnumerator<float> Timer(Player player)
        {
            float refreshRate = Math.Max(0.2f, plugin.Config.TimerRefreshRate);

            while (player != null && player.IsConnected && states.TryGetValue(player, out ProtectionState state))
            {
                if (!state.HasTeamProtection)
                {
                    states.Remove(player);
                    yield break;
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
                    player.ShowHint(text, refreshRate + 0.15f);

                yield return Timing.WaitForSeconds(refreshRate);
            }
        }
    }
}
