using Exiled.API.Features;

namespace SpawnProtection.API
{
    public static class SpawnProtectionApi
    {
        public static void ApplyProtection(Player player)
        {
            Plugin.Instance?.ProtectionManager?.Apply(player);
        }

        public static void RemoveProtection(Player player)
        {
            Plugin.Instance?.ProtectionManager?.Remove(player);
        }

        public static bool HasFullProtection(Player player)
        {
            return Plugin.Instance?.ProtectionManager?.HasFullProtection(player) == true;
        }

        public static bool HasTeamProtection(Player player)
        {
            return Plugin.Instance?.ProtectionManager?.HasTeamProtection(player) == true;
        }
    }
}
