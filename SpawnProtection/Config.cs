using System.ComponentModel;
using Exiled.API.Interfaces;

namespace SpawnProtection
{
    public sealed class Config : IConfig
    {
        [Description("Whether the plugin is enabled.")]
        public bool IsEnabled { get; set; } = true;

        [Description("Whether debug messages are shown in the server console.")]
        public bool Debug { get; set; } = false;

        [Description("Maximum duration of full player-damage immunity for all eligible roles except Chaos, in seconds.")]
        public float FullProtectionDuration { get; set; } = 10f;

        [Description("Maximum duration of full player-damage immunity for Chaos Insurgency, in seconds.")]
        public float ChaosFullProtectionDuration { get; set; } = 8f;

        [Description("Duration of team-damage protection from the moment of spawning, in seconds.")]
        public float TeamProtectionDuration { get; set; } = 60f;

        [Description("Remove full protection as soon as the protected player successfully attacks another player.")]
        public bool RemoveFullProtectionOnAttack { get; set; } = true;

        [Description("Show a small protection timer using HintServiceMeow.")]
        public bool ShowTimer { get; set; } = true;

        [Description("How often the timer text is refreshed, in seconds.")]
        public float TimerRefreshRate { get; set; } = 0.25f;

        [Description("Horizontal HUD coordinate. Higher values move the timer to the right.")]
        public float HudXCoordinate { get; set; } = 1080f;

        [Description("Vertical HUD coordinate. Higher values move the timer lower on the screen.")]
        public float HudYCoordinate { get; set; } = 970f;

        [Description("HUD font size.")]
        public int HudFontSize { get; set; } = 16;

        [Description("Text shown while full protection is active. Available placeholder: {time}.")]
        public string FullProtectionHint { get; set; } = "<color=#55CCFF>Vollschutz: {time}s</color>";

        [Description("Text shown while only team protection is active. Available placeholder: {time}.")]
        public string TeamProtectionHint { get; set; } = "<color=#66FF99>Teamschutz: {time}s</color>";

        [Description("Message shown when full protection ends because the player attacked.")]
        public string AttackEndedHint { get; set; } = "<color=#FFD166>Vollschutz beendet: Du hast angegriffen.</color>";

        [Description("Duration of the attack-ended message, in seconds.")]
        public float AttackEndedHintDuration { get; set; } = 3f;
    }
}
