using System;
using Exiled.API.Features;
using Player = Exiled.Events.Handlers.Player;

namespace SpawnProtection
{
    public sealed class Plugin : Plugin<Config>
    {
        public static Plugin Instance { get; private set; }

        public override string Name => "SpawnProtection";
        public override string Author => "Herzog-XI";
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredExiledVersion => new Version(9, 14, 2);

        internal EventHandlers Handlers { get; private set; }
        internal ProtectionManager ProtectionManager { get; private set; }

        public override void OnEnabled()
        {
            Instance = this;
            ProtectionManager = new ProtectionManager(this);
            Handlers = new EventHandlers(ProtectionManager);

            Player.Spawned += Handlers.OnSpawned;
            Player.Hurting += Handlers.OnHurting;
            Player.Died += Handlers.OnDied;
            Player.ChangingRole += Handlers.OnChangingRole;
            Exiled.Events.Handlers.Server.RestartingRound += Handlers.OnRestartingRound;

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Player.Spawned -= Handlers.OnSpawned;
            Player.Hurting -= Handlers.OnHurting;
            Player.Died -= Handlers.OnDied;
            Player.ChangingRole -= Handlers.OnChangingRole;
            Exiled.Events.Handlers.Server.RestartingRound -= Handlers.OnRestartingRound;

            ProtectionManager?.ClearAll();
            Handlers = null;
            ProtectionManager = null;
            Instance = null;

            base.OnDisabled();
        }
    }
}
