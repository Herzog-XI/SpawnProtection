using System;
using System.Collections.Generic;
using Exiled.API.Features;
using MEC;
using PlayerRoles;

namespace SpawnProtection
{
    public static class ProtectionManager
    {
        private static readonly Dictionary<int, ProtectionState> States = new Dictionary<int, ProtectionState>();

        public static bool IsEligible(Player player)
        {
            if (player == null || !player.IsConnected)
                return false;

            RoleTypeId role = player.Role.Type;
            return role != RoleTypeId.None &&
                   role != RoleTypeId.Spectator &&
                   role != RoleTypeId.Overwatch &&
                   !player.IsScp;
        }

        public static void Apply(Player player)
        {
            Remove(player);

            if (!IsEligible(player) || Plugin.Instance == null)
                return;

            Config config = Plugin.Instance.Config;
            DateTime now = DateTime.UtcNow;
            var state = new ProtectionState
            {
                SpawnedAt = now,
                FullProtectionEndsAt = now.AddSeconds(Math.Max(0f, config.FullProtectionDuration)),
                TeamProtectionEndsAt = now.AddSeconds(Math.Max(0f, config.TeamProtectionDuration)),
                FullProtectionRemovedByAttack = false,
            };

            States[player.Id] = state;

            if (config.ShowTimer)
                state.TimerCoroutine = Timing.RunCoroutine(TimerCoroutine(player));
        }

        public static void Remove(Player player)
        {
            if (player == null || !States.TryGetValue(player.Id, out ProtectionState state))
                return;

            if (state.TimerCoroutine.IsRunning)
                Timing.KillCoroutines(state.TimerCoroutine);

            States.Remove(player.Id);
        }

        public static bool HasFullProtection(Player player)
        {
            return TryGetActiveState(player, out ProtectionState state) &&
                   !state.FullProtectionRemovedByAttack &&
                   DateTime.UtcNow < state.FullProtectionEndsAt;
        }

        public static bool HasTeamProtection(Player player)
        {
            return TryGetActiveState(player, out ProtectionState state) &&
                   DateTime.UtcNow < state.TeamProtectionEndsAt;
        }

        public static void RemoveFullProtectionBecauseOfAttack(Player player)
        {
            if (player == null || !States.TryGetValue(player.Id, out ProtectionState state))
                return;

            if (!HasFullProtection(player))
                return;

            state.FullProtectionRemovedByAttack = true;
            string message = Plugin.Instance?.Config.AttackEndedProtectionText;
            if (!string.IsNullOrWhiteSpace(message))
                player.ShowHint(message, 2f);
        }

        public static void ClearAll()
        {
            foreach (ProtectionState state in States.Values)
            {
                if (state.TimerCoroutine.IsRunning)
                    Timing.KillCoroutines(state.TimerCoroutine);
            }

            States.Clear();
        }

        private static bool TryGetActiveState(Player player, out ProtectionState state)
        {
            state = null;
            if (player == null || !States.TryGetValue(player.Id, out state))
                return false;

            if (!IsEligible(player) || DateTime.UtcNow >= state.TeamProtectionEndsAt)
            {
                Remove(player);
                state = null;
                return false;
            }

            return true;
        }

        private static IEnumerator<float> TimerCoroutine(Player player)
        {
            while (player != null && player.IsConnected && TryGetActiveState(player, out ProtectionState state))
            {
                Config config = Plugin.Instance.Config;
                int full = Math.Max(0, (int)Math.Ceiling((state.FullProtectionEndsAt - DateTime.UtcNow).TotalSeconds));
                int team = Math.Max(0, (int)Math.Ceiling((state.TeamProtectionEndsAt - DateTime.UtcNow).TotalSeconds));

                string text = HasFullProtection(player)
                    ? config.FullProtectionText.Replace("{full}", full.ToString()).Replace("{team}", team.ToString())
                    : config.TeamProtectionText.Replace("{team}", team.ToString());

                if (!string.IsNullOrWhiteSpace(text))
                    player.ShowHint(text, Math.Max(1.1f, config.TimerRefreshRate + 0.2f));

                yield return Timing.WaitForSeconds(Math.Max(0.25f, config.TimerRefreshRate));
            }
        }
    }
}
