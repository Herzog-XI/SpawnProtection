using System.ComponentModel;
using Exiled.API.Interfaces;

namespace SpawnProtection
{
    public sealed class Config : IConfig
    {
        [Description("Whether SpawnProtection is enabled.")]
        public bool IsEnabled { get; set; } = true;

        [Description("Whether debug messages are shown in the server console.")]
        public bool Debug { get; set; } = false;

        [Description("Maximum duration in seconds of full damage immunity after spawning.")]
        public float FullProtectionDuration { get; set; } = 15f;

        [Description("Duration in seconds of team-damage protection, counted from spawning.")]
        public float TeamProtectionDuration { get; set; } = 60f;

        [Description("Whether full protection ends when the protected player damages another player.")]
        public bool RemoveFullProtectionOnAttack { get; set; } = true;

        [Description("Whether the protection timer is displayed to protected players.")]
        public bool ShowTimer { get; set; } = true;

        [Description("How often the timer is refreshed, in seconds.")]
        public float TimerRefreshRate { get; set; } = 1f;

        [Description("Hint shown while full protection is active. Available placeholders: {full}, {team}.")]
        public string FullProtectionText { get; set; } = "<align=right><voffset=-7em><size=20><color=#65D7FF>🛡 Vollschutz: {full}s</color>\n<color=#9DFF9D>🤝 Teamschutz: {team}s</color></size></voffset></align>";

        [Description("Hint shown while only team protection is active. Available placeholder: {team}.")]
        public string TeamProtectionText { get; set; } = "<align=right><voffset=-7em><size=20><color=#9DFF9D>🤝 Teamschutz: {team}s</color></size></voffset></align>";

        [Description("Brief message shown when full protection ends because the player attacked.")]
        public string AttackEndedProtectionText { get; set; } = "<color=#FFD166>Vollschutz beendet – du hast angegriffen.</color>";
    }
}
