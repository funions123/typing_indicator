using System.Text.Json;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;

namespace TypingIndicator
{
    public class EntityBehaviorTypingIndicator : EntityBehavior
    {
        ICoreAPI api;

        public bool IsTyping
        {
            get
            {
                return entity.WatchedAttributes.GetTreeAttribute(PropertyName()).GetBool("typing", false);
            }
            set
            {
                entity.WatchedAttributes.GetTreeAttribute(PropertyName())?.SetBool("typing", value);
            }
        }

        public string TypingIndicatorText = new Configuration().Localizations["en"];

        public string ChatText;

        public float TimeSinceLastTyping = 0;
        public float MaxTimeSinceLastTyping = 5;

        public GuiDialog ChatDialog;
        public GuiElementChatInput ChatInput;
        public bool ServerConfigUpdated = false;

        public int RenderRange
        {
            get
            {
                return entity.WatchedAttributes.GetTreeAttribute("typingindicator").GetInt("renderRange");
            }
            set
            {
                entity.WatchedAttributes.GetTreeAttribute("typingindicator")?.SetInt("renderRange", value);
            }
        }

        public EntityBehaviorTypingIndicator(Entity entity) : base(entity)
        {
            api = entity.Api;

            entity.WatchedAttributes.SetAttribute(PropertyName(), new TreeAttribute());
            IsTyping = false;
            RenderRange = 100;
            TimeSinceLastTyping = 0;
            ChatText = string.Empty;
        }

        public void MarkDirty()
        {
            entity.WatchedAttributes.MarkPathDirty("typingindicator");
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data, ref EnumHandling handled)
        {
            base.OnReceivedServerPacket(packetid, data, ref handled);

            if(api is ICoreClientAPI capi)
            {
                if(packetid == (int)PACKET_ID.PacketID_PlayerTyping)
                {                    
                    var packet = SerializerUtil.Deserialize<Packet_PlayerTyping>(data);
                    var nearbyPlayers = capi.World.GetPlayersAround(capi.World.Player.Entity.Pos.XYZ, RenderRange, RenderRange).Where(p => p.Entity.EntityId != capi.World.Player.Entity.EntityId);

                    nearbyPlayers.Foreach(p => {
                        if(p.Entity.EntityId == packet.entityId)
                        {
                            p.Entity.GetBehavior<EntityBehaviorTypingIndicator>().IsTyping = packet.isTyping;
                        }
                    });
                    return;
                }

                if(packetid == (int)PACKET_ID.PacketID_ReceivedServerConfig)
                {
                    var serverConfig = SerializerUtil.Deserialize<Packet_ServerConfig>(data);
                    bool hasLanguageCode = serverConfig.ServerLocalizations.ContainsKey(serverConfig.PlayerLanguageCode);

                    if(hasLanguageCode == false)
                    {
                        if(capi.World.Player.Entity.EntityId == entity.EntityId)
                        {
                            capi.ShowChatMessage("The server's localizations for Typing Indicator does not contain your selected language. We'll use the default, English.");
                        }
                        TypingIndicatorText = new Configuration().Localizations["en"];
                    }
                    else
                    {
                        TypingIndicatorText = serverConfig.ServerLocalizations[serverConfig.PlayerLanguageCode];
                    }
                    
                    RenderRange = serverConfig.MaxRange;
                    MaxTimeSinceLastTyping = serverConfig.MaxTimeout;
                    ServerConfigUpdated = true;
                    return;
                }
            }
        }

        public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data, ref EnumHandling handled)
        {
            base.OnReceivedClientPacket(player, packetid, data, ref handled);

            if(api is ICoreServerAPI sapi)
            {
                if(packetid == (int)PACKET_ID.PacketID_PlayerTyping)
                {            
                    sapi.Network.BroadcastEntityPacket(entity.EntityId, (int)PACKET_ID.PacketID_PlayerTyping, data);
                    return;
                }

                if(packetid == (int)PACKET_ID.PacketID_RequestServerConfig)
                {
                    Packet_ServerConfig serverConfig = GetConfigurationForServerPlayer(player);
                    sapi.Network.SendEntityPacket(player, entity.EntityId, (int)PACKET_ID.PacketID_ReceivedServerConfig, SerializerUtil.Serialize(serverConfig));
                    return;
                }
            }
        }

        public Packet_ServerConfig GetConfigurationForServerPlayer(IServerPlayer player)
        {
            bool shouldUpgrade = false;
            var configPath = api.GetOrCreateDataPath($"{api.DataBasePath}/ModConfig/typing_indicator");
            var configFilePath = $"{configPath}/config.json";
            var configOutOfDatePath = $"{configPath}/out_of_date_configs";

            //Ensure directories.
            Directory.CreateDirectory(configPath);
            Directory.CreateDirectory(configOutOfDatePath);

            var currentVersion = api.ModLoader.GetMod(TypingIndicator.ModID).Info.Version;
            Configuration? serverConfiguration = api.LoadModConfig<Configuration>(configFilePath);

            if(serverConfiguration != null)
            {
                var savedVersion = serverConfiguration.READONLY_CreatedWithTypingIndicatorVersion;
                if(savedVersion != currentVersion)
                {
                    // api.StoreModConfig(JsonConvert.SerializeObject(serverConfiguration), $"{configOutOfDatePath}/config_{serverConfiguration.READONLY_CreatedWithTypingIndicatorVersion}.json");
                    
                    File.Move(configFilePath, $"{configOutOfDatePath}/config_{serverConfiguration.READONLY_CreatedWithTypingIndicatorVersion}.json");
                    api.Logger.Error($"""
                    Typing Indicator has updated it's version and there may have been some changes to the JSON format of the configuration.
                    Please see {configPath} and see if the new config.json file is busted.
                    We have moved your old configuration to:
                    {configOutOfDatePath}/config_{serverConfiguration.READONLY_CreatedWithTypingIndicatorVersion}.json

                    Love ya! - Lila (Fatigue)
                    """);
                    shouldUpgrade = true;
                }
            }

            if(serverConfiguration == null || shouldUpgrade)
            {
                Configuration defaultConfig = new Configuration() {READONLY_CreatedWithTypingIndicatorVersion = currentVersion};                
                File.WriteAllText(configFilePath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
                return defaultConfig.ToServerConfigPacket(player.LanguageCode);
            }

            return serverConfiguration.ToServerConfigPacket(player.LanguageCode);//[player.LanguageCode] ?? "Typing";
        }

        public override void FromBytes(bool isSync)
        {
            base.FromBytes(isSync);            
        }

        public void TryGetChatGUI(ICoreClientAPI clapi)
        {
            var dialog = from d in clapi.Gui.OpenedGuis where d.GetType() == typeof(HudDialogChat) select d;
            if(dialog != null)
            {
                var chatDialog = dialog.FirstOrDefault(defaultValue: null);
                if(chatDialog == null) return;
                
                ChatDialog = chatDialog;
                ChatInput = ChatDialog.Composers["chat"].GetChatInput("chatinput");
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            if(api.Side != EnumAppSide.Client) return;

            if(api is ICoreClientAPI capi)
            {
                if(capi.World.Player.Entity.EntityId != entity.EntityId) return;

                if(ChatDialog == null || ChatInput == null)
                {
                    TryGetChatGUI(capi);
                    return;
                }

                if(TimeSinceLastTyping < MaxTimeSinceLastTyping)
                {
                    TimeSinceLastTyping += deltaTime;
                }

                if(ChatText != ChatInput.GetText())
                {
                    TimeSinceLastTyping = 0;
                    ChatText = ChatInput.GetText();
                }

                if(ChatInput.HasFocus
                    && ShouldStopTyping() == false
                    && string.IsNullOrEmpty(ChatText) == false
                    // && ChatText.StartsWith('.') == false // These may be used by roleplay servers. /me, /it and so on.
                    // && ChatText.StartsWith('/') == false
                    )
                {
                    SetTyping(true);
                }
                else
                {
                    SetTyping(false);
                }
            }

            // if(TimeSinceLastTyping > MaxTimeSinceLastTyping && !HasSentTimeoutPacket)
            // {
            //     SetTyping(false);
            //     HasSentTimeoutPacket = true;
            // }
            // else
            // {
            //     TimeSinceLastTyping += deltaTime;
            // }
        }

        public bool ShouldStopTyping()
        {
            return TimeSinceLastTyping >= MaxTimeSinceLastTyping;
        }

        /// <summary>
        /// Sets the value of IsTyping and sends to the server if it's been changed
        /// </summary>
        /// <param name="typing"></param>
        public void SetTyping(bool typing)
        {
            if(IsTyping == typing) return;

            IsTyping = typing;

            MarkDirty();

            if(api is ICoreClientAPI capi)
            {
                // capi.ShowChatMessage($"Set typing to {typing}");

                var packet = SerializerUtil.Serialize(new Packet_PlayerTyping(){isTyping = typing, entityId = entity.EntityId});

                if(packet == null) capi.ShowChatMessage("The packet was null");

                capi.Network.SendEntityPacket(entity.EntityId, (int)PACKET_ID.PacketID_PlayerTyping, packet);
            }
        }

        public override string PropertyName()
        {
            return "typingindicator";
        }
    }
}