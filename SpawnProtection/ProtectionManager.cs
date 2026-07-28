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
        private MethodInfo playerDisplayFactoryMethod;
        private MethodInfo addHintMethod;
        private MethodInfo removeHintMethod;
        private bool hsmResolved;
        private bool hsmWarningShown;

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
                int seconds = Math.Max(0, (int)Math.Ceiling((state.FullProtectionEndsAt - DateTime.UtcNow).TotalSeconds));
                text = plugin.Config.FullProtectionHint?.Replace("{time}", seconds.ToString());
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
                    if (!hsmWarningShown)
                    {
                        hsmWarningShown = true;
                        Log.Warn("[SpawnProtection] HintServiceMeow API was not found. Protection works, but the HUD timer is disabled.");
                    }

                    return null;
                }

                object hint = Activator.CreateInstance(hintType);
                SetProperty(hint, "Id", "spawn_protection_hud");
                SetProperty(hint, "Text", string.Empty);
                SetProperty(hint, "XCoordinate", plugin.Config.HudXCoordinate);
                SetProperty(hint, "YCoordinate", plugin.Config.HudYCoordinate);
                SetEnumProperty(hint, "YCoordinateAlign", "Bottom");
                SetEnumProperty(hint, "Alignment", "Center");
                SetProperty(hint, "FontSize", Math.Max(1, plugin.Config.HudFontSize));
                SetEnumProperty(hint, "SyncSpeed", "Fast");

                object display = GetPlayerDisplay(player);
                if (display == null)
                    throw new InvalidOperationException("HintServiceMeow returned no PlayerDisplay for the player.");

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

                object display = GetPlayerDisplay(player);
                if (display != null)
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
                return true;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (Assembly assembly in assemblies)
            {
                hintType = assembly.GetType("HintServiceMeow.Core.Models.Hints.Hint", false);
                if (hintType != null)
                    break;
            }

            foreach (Assembly assembly in assemblies)
            {
                playerDisplayType = assembly.GetType("HintServiceMeow.Core.Utilities.PlayerDisplay", false);
                if (playerDisplayType != null)
                    break;
            }

            if (hintType == null || playerDisplayType == null)
                return false;

            playerDisplayFactoryMethod = FindPlayerDisplayFactory(assemblies);
            if (playerDisplayFactoryMethod == null)
                return false;

            addHintMethod = playerDisplayType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(method => method.Name == "AddHint" && method.GetParameters().Length == 1);
            removeHintMethod = playerDisplayType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(method => method.Name == "RemoveHint" && method.GetParameters().Length == 1);

            if (addHintMethod == null || removeHintMethod == null)
                return false;

            hsmResolved = true;
            Log.Info($"[SpawnProtection] HintServiceMeow HUD API detected through {playerDisplayFactoryMethod.DeclaringType?.FullName}.{playerDisplayFactoryMethod.Name}.");
            return true;
        }

        private MethodInfo FindPlayerDisplayFactory(IEnumerable<Assembly> assemblies)
        {
            MethodInfo directMethod = playerDisplayType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                    (method.Name == "Get" || method.Name == "GetPlayerDisplay")
                    && method.GetParameters().Length == 1
                    && playerDisplayType.IsAssignableFrom(method.ReturnType));

            if (directMethod != null)
                return directMethod;

            foreach (Assembly assembly in assemblies.Where(item =>
                item.GetName().Name.IndexOf("HintServiceMeow", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                foreach (Type type in GetLoadableTypes(assembly))
                {
                    MethodInfo extensionMethod = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(method =>
                            (method.Name == "GetPlayerDisplay" || method.Name == "Get")
                            && method.GetParameters().Length == 1
                            && playerDisplayType.IsAssignableFrom(method.ReturnType));

                    if (extensionMethod != null)
                        return extensionMethod;
                }
            }

            return null;
        }

        private object GetPlayerDisplay(Player player)
        {
            ParameterInfo parameter = playerDisplayFactoryMethod.GetParameters()[0];
            object argument = ResolvePlayerArgument(player, parameter.ParameterType);
            if (argument == null)
                throw new InvalidOperationException($"Cannot convert EXILED player to {parameter.ParameterType.FullName} for HintServiceMeow.");

            return playerDisplayFactoryMethod.Invoke(null, new[] { argument });
        }

        private static object ResolvePlayerArgument(Player player, Type targetType)
        {
            if (targetType.IsInstanceOfType(player))
                return player;

            PropertyInfo referenceHubProperty = player.GetType().GetProperty("ReferenceHub", BindingFlags.Public | BindingFlags.Instance);
            object referenceHub = referenceHubProperty?.GetValue(player);
            if (referenceHub != null && targetType.IsInstanceOfType(referenceHub))
                return referenceHub;

            PropertyInfo gameObjectProperty = player.GetType().GetProperty("GameObject", BindingFlags.Public | BindingFlags.Instance);
            object gameObject = gameObjectProperty?.GetValue(player);
            if (gameObject != null && targetType.IsInstanceOfType(gameObject))
                return gameObject;

            return null;
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
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
