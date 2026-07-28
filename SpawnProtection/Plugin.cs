using System;
using Exiled.API.Features;
using Player = Exiled.Events.Handlers.Player;
using Server = Exiled.Events.Handlers.Server;

namespace SpawnProtection
{
    public sealed class Plugin : Plugin<Config>
    {
        public static Plugin Instance { get; private set; }

        public override string Name => "SpawnProtection";
        public override string Author => "Herzog-XI";
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredExiledVersion => new Version(9, 0, 0);

        private EventHandlers handlers;

        public override void OnEnabled()
        {
            Instance = this;
            handlers = new EventHandlers();

            Player.Verified += handlers.OnVerified;
            Player.ChangingRole += handlers.OnChangingRole;
            Player.Hurting += handlers.OnHurting;
            Player.Died += handlers.OnDied;
            Player.Destroying += handlers.OnDestroying;
            Server.RoundEnded += handlers.OnRoundEnded;
            Server.RestartingRound += handlers.OnRestartingRound;

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Player.Verified -= handlers.OnVerified;
            Player.ChangingRole -= handlers.OnChangingRole;
            Player.Hurting -= handlers.OnHurting;
            Player.Died -= handlers.OnDied;
            Player.Destroying -= handlers.OnDestroying;
            Server.RoundEnded -= handlers.OnRoundEnded;
            Server.RestartingRound -= handlers.OnRestartingRound;

            ProtectionManager.ClearAll();
            handlers = null;
            Instance = null;

            base.OnDisabled();
        }
    }
}
