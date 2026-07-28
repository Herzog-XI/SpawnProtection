using Exiled.API.Features;

namespace SpawnProtection
{
    public static class SpawnProtectionApi
    {
        public static void ApplyProtection(Player player) => ProtectionManager.Apply(player);

        public static void RemoveProtection(Player player) => ProtectionManager.Remove(player);

        public static bool HasFullProtection(Player player) => ProtectionManager.HasFullProtection(player);

        public static bool HasTeamProtection(Player player) => ProtectionManager.HasTeamProtection(player);
    }
}
