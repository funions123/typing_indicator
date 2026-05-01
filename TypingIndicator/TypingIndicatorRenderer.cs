using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace TypingIndicator
{
	public class EntityTypingIndicatorRenderer : IRenderer
	{
		public double RenderOrder => -0.1;

		public int RenderRange => 999;

		public double[] color = ColorUtil.WhiteArgbDouble;

		public TextBackground background = new TextBackground
		{
			FillColor = GuiStyle.DialogLightBgColor,
			Padding = 2,
			Radius = GuiStyle.ElementBGRadius,
			Shade = true,
			BorderColor = GuiStyle.DialogBorderColor,
			BorderWidth = 3.0
		};

		private ICoreClientAPI capi;

		private Entity entity;

		public LoadedTexture? texture;

		public string DisplayText = new Configuration().Localizations["en"];
		public int RenderDistance = 100;

		public EntityTypingIndicatorRenderer(ICoreClientAPI capi, Entity entity)
		{
			this.capi = capi;
			this.entity = entity;
		}

		public LoadedTexture? GenerateTypingTexture(ICoreClientAPI capi, Entity entity)
		{
			var indicator = entity.GetBehavior<EntityBehaviorTypingIndicator>();

			if(indicator == null) return null;
		
			if(indicator.ServerConfigUpdated)
			{
				DisplayText = indicator.TypingIndicatorText;
				RenderDistance = indicator.RenderRange;
				indicator.ServerConfigUpdated = false;
			}

			if (indicator.IsTyping)
			{
				if(texture == null)
				{
					return texture = capi.Gui.TextTexture.GenUnscaledTextTexture(DisplayText, CairoFont.WhiteSmallText().WithColor(color), background);
				}
				return texture;
			}
			return null;
		}

		public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
		{
			EntityPlayer? obj = entity as EntityPlayer;

			if (obj == null) return;

			IRenderAPI rapi = capi.Render;
			EntityPlayer entityPlayer = capi.World.Player.Entity;
			Vec3d typingIndicatorPos;
			
			if (capi.World.Player.Entity.EntityId == entity.EntityId)
			{
				if (rapi.CameraType == EnumCameraMode.FirstPerson) return;
				typingIndicatorPos = new Vec3d(entityPlayer.CameraPos.X + entityPlayer.LocalEyePos.X, entityPlayer.CameraPos.Y + 0.3 + entityPlayer.LocalEyePos.Y, entityPlayer.CameraPos.Z + entityPlayer.LocalEyePos.Z);
			}
			else
			{
				IMountableSeat? thisMount = (entity as EntityAgent)?.MountedOn;
				IMountableSeat? selfMount = entityPlayer.MountedOn;
				if (thisMount?.MountSupplier != null && thisMount.MountSupplier == selfMount?.MountSupplier)
				{
					Vec3f mpos = thisMount.Config.RiderOffset;
					typingIndicatorPos = new Vec3d(entityPlayer.CameraPos.X + entityPlayer.LocalEyePos.X, entityPlayer.CameraPos.Y + 0.3 + entityPlayer.LocalEyePos.Y, entityPlayer.CameraPos.Z + entityPlayer.LocalEyePos.Z);
					typingIndicatorPos.Add(mpos);
				}
				else
				{
					typingIndicatorPos = new Vec3d(entity.Pos.X, entity.Pos.Y + (double)entity.SelectionBox.Y2 + 0.1, entity.Pos.Z);
				}
			}

			double dist = entityPlayer.Pos.SquareDistanceTo(entity.Pos);

			if(dist > RenderDistance) return;

			double offX = entity.SelectionBox.X2 - entity.OriginSelectionBox.X2;
			double offZ = entity.SelectionBox.Z2 - entity.OriginSelectionBox.Z2;
			typingIndicatorPos.Add(offX, 0.0, offZ);
			Vec3d pos = MatrixToolsd.Project(typingIndicatorPos, rapi.PerspectiveProjectionMat, rapi.PerspectiveViewMat, rapi.FrameWidth, rapi.FrameHeight);

			if (pos.Z < 0.0) return;

			LoadedTexture? tex = GenerateTypingTexture(capi, obj);

			if (tex != null)
			{
				float scale = 4f / Math.Max(1f, (float)pos.Z);
				float cappedScale = Math.Min(1f, scale);
				
				float posx2 = (float)pos.X - cappedScale * (float)tex.Width / 2f;
				float posy3 = (float)rapi.FrameHeight - (float)pos.Y - (float)tex.Height / 2 * Math.Max(0f, cappedScale);
				rapi.Render2DTexture(tex.TextureId, posx2, posy3, tex.Width, tex.Height);
			}
		}

		public void Dispose()
		{

		}
	}
}