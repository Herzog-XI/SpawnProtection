using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        private Type hintType;
        private Type playerDisplayType;
        private MethodInfo playerDisplayGetMethod;
        private MethodInfo addHintMethod;
        private MethodInfo removeHintMethod;
        private bool hsmResolved;

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
                state.HudHint = CreateHud(player);

            lock (syncRoot)
                states[player] = state;

            if (plugin.Config.ShowTimer && state.HudHint != null)
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

            SetProperty(state.HudHint, "Text", text ?? string.Empty);
        }

        private object CreateHud(Player player)
        {
            try
            {
                if (!ResolveHintServiceMeow())
                {
                    Log.Warn("[SpawnProtection] HintServiceMeow-Exiled was not found. Protection works, but the HUD timer is disabled.");
                    return null;
                }

                object hint = Activator.CreateInstance(hintType);
                SetProperty(hint, "Id", "spawn_protection_hud");
                SetProperty(hint, "Text", string.Empty);
                SetProperty(hint, "XCoordinate", plugin.Config.HudXCoordinate);
                SetProperty(hint, "YCoordinate", plugin.Config.HudYCoordinate);
                SetEnumProperty(hint, "YCoordinateAlign", "Bottom");
                SetEnumProperty(hint, "Alignment", "Right");
                SetProperty(hint, "FontSize", Math.Max(1, plugin.Config.HudFontSize));
                SetEnumProperty(hint, "SyncSpeed", "Fast");

                object display = playerDisplayGetMethod.Invoke(null, new object[] { player });
                addHintMethod.Invoke(display, new[] { hint });
                return hint;
            }
            catch (Exception exception)
            {
                Log.Error($"[SpawnProtection] Could not create HintServiceMeow HUD: {exception}");
                return null;
            }
        }

        private void RemoveHud(Player player, ProtectionState state)
        {
            if (player == null || state?.HudHint == null)
                return;

            try
            {
                if (!ResolveHintServiceMeow())
                    return;

                object display = playerDisplayGetMethod.Invoke(null, new object[] { player });
                removeHintMethod.Invoke(display, new[] { state.HudHint });
            }
            catch
            {
                // Player may already be disconnected during cleanup.
            }
        }

        private bool ResolveHintServiceMeow()
        {
            if (hsmResolved)
                return hintType != null;

            hsmResolved = true;
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(item => item.GetName().Name.Equals("HintServiceMeow-Exiled", StringComparison.OrdinalIgnoreCase));

            if (assembly == null)
                return false;

            hintType = assembly.GetType("HintServiceMeow.Core.Models.Hints.Hint");
            playerDisplayType = assembly.GetType("HintServiceMeow.Core.Utilities.PlayerDisplay");
            if (hintType == null || playerDisplayType == null)
                return false;

            playerDisplayGetMethod = playerDisplayType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "Get" && method.GetParameters().Length == 1);

            if (playerDisplayGetMethod == null)
                return false;

            Type displayType = playerDisplayGetMethod.ReturnType;
            addHintMethod = displayType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(method => method.Name == "AddHint" && method.GetParameters().Length == 1);
            removeHintMethod = displayType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(method => method.Name == "RemoveHint" && method.GetParameters().Length == 1);

            return addHintMethod != null && removeHintMethod != null;
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite)
                return;

            Type targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            object converted = value;
            if (value != null && !targetType.IsInstanceOfType(value))
                converted = Convert.ChangeType(value, targetType);

            property.SetValue(target, converted);
        }

        private static void SetEnumProperty(object target, string propertyName, string enumValue)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
                return;

            object value = Enum.Parse(property.PropertyType, enumValue, true);
            property.SetValue(target, value);
        }
    }
}