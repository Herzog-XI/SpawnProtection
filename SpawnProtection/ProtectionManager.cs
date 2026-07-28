using System;
using System.Collections.Generic;
using System.Threading;
using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Models.Hints;
using HintServiceMeow.Core.Utilities;
using PlayerRoles;
using HsmHint = HintServiceMeow.Core.Models.Hints.Hint;

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

            float fullProtectionDuration = player.Role.Team == Team.ChaosInsurgency
                ? plugin.Config.ChaosFullProtectionDuration
                : plugin.Config.FullProtectionDuration;

            DateTime now = DateTime.UtcNow;
            ProtectionState state = new ProtectionState
            {
                FullProtectionEndsAt = now.AddSeconds(Math.Max(0f, fullProtectionDuration)),
                TeamProtectionEndsAt = now.AddSeconds(Math.Max(0f, plugin.Config.TeamProtectionDuration)),
                FullProtectionRemoved = fullProtectionDuration <= 0f,
            };

            if (plugin.Config.ShowTimer)
            {
                state.HudHint = new HsmHint
                {
                    Id = "spawn_protection_hud",
                    Text = string.Empty,
                    XCoordinate = plugin.Config.HudXCoordinate,
                    YCoordinate = plugin.Config.HudYCoordinate,
                    YCoordinateAlign = HintVerticalAlign.Bottom,
                    Alignment = HintAlignment.Right,
                    FontSize = Math.Max(1, plugin.Config.HudFontSize),
                    SyncSpeed = HintSyncSpeed.Fast,
                };

                PlayerDisplay.Get(player).AddHint(state.HudHint);
            }

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

            RemoveHud(player, state);
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

            UpdateTimer(player);

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
            List<KeyValuePair<Player, ProtectionState>> activeStates;
            lock (syncRoot)
            {
                activeStates = new List<KeyValuePair<Player, ProtectionState>>(states);
                states.Clear();
            }

            foreach (KeyValuePair<Player, ProtectionState> entry in activeStates)
            {
                RemoveHud(entry.Key, entry.Value);
                entry.Value.Dispose();
            }
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

            if (state.HudHint == null)
                return;

            string text;
            if (state.HasFullProtection)
            {
                int fullSeconds = Math.Max(0, (int)Math.Ceiling((state.FullProtectionEndsAt - DateTime.UtcNow).TotalSeconds));
                int teamSeconds = Math.Max(0, (int)Math.Ceiling((state.TeamProtectionEndsAt - DateTime.UtcNow).TotalSeconds));
                string fullText = plugin.Config.FullProtectionHint?.Replace("{time}", fullSeconds.ToString());
                string teamText = plugin.Config.TeamProtectionHint?.Replace("{time}", teamSeconds.ToString());
                text = $"{fullText}\n{teamText}";
            }
            else
            {
                int seconds = Math.Max(0, (int)Math.Ceiling((state.TeamProtectionEndsAt - DateTime.UtcNow).TotalSeconds));
                text = plugin.Config.TeamProtectionHint?.Replace("{time}", seconds.ToString());
            }

            state.HudHint.Text = text ?? string.Empty;
        }

        private static void RemoveHud(Player player, ProtectionState state)
        {
            if (player == null || state?.HudHint == null)
                return;

            try
            {
                PlayerDisplay.Get(player).RemoveHint(state.HudHint);
            }
            catch
            {
                // The player may already be disconnected while the timer is being cleaned up.
            }
        }
    }
}