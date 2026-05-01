using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TypingIndicator
{
    class TypingIndicator : ModSystem
    {
        ICoreClientAPI? capi;

        public static string ModID { get; private set; } = "";

        // Dictionary<string, string> typingIndicatorLocalizations;

        public override void Start(ICoreAPI api)
        {
            ModID = Mod.Info.ModID;
            api.RegisterEntityBehaviorClass("typingindicator", typeof(EntityBehaviorTypingIndicator));
            base.Start(api);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            api.Event.PlayerEntitySpawn += (player) =>
            {
                player.Entity.AddBehavior(new EntityBehaviorTypingIndicator(player.Entity));
                api.Event.RegisterRenderer(new EntityTypingIndicatorRenderer(api, player.Entity), EnumRenderStage.Ortho);
                api.Network.SendEntityPacket(player.Entity.EntityId, (int)PACKET_ID.PacketID_RequestServerConfig, null);
            };

            base.StartClientSide(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
        }
    }
}
