using System;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace editjournal.src
{

    /**
     * Seems to work fine for now, IIgnitable is annoying to hook into because Tyron checks the object as a cast instead of getting the interface and hard coded the item conversion
     * 
     * Block interface IIgnitable has 3 methods
     * 1 - Stack for when a stack is being rubbed against it
     * 2 - Block for when a burning thing is rubbing against the placed version
     * 3 - And a method called when the block is finished being interacted with from a firestarter or CanIgnite block behavior
     * 
     * BlockBehaviorCanIgnite selects the <IIgnitable> through .GetBlock(blockSel.Position).GetInterface<IIgnitable>(byEntity.World, blockSel.Position);
     */


    //[HarmonyPatch]
    //public class TorchPatches : ModSystem
    //{
    //    public static ICoreAPI api;
    //    public Harmony harmony;

    //    private static readonly string _harmonyID = "Soggylithe.Reignited.v1.2.0";

    //    public override void Start(ICoreAPI api)
    //    {
    //        TorchPatches.api = api;

    //        if (!Harmony.HasAnyPatches(_harmonyID))
    //        {
    //            harmony = new Harmony(_harmonyID);
    //            harmony.PatchAll();
    //        }

    //        base.Start(api);
    //    }

    //    [HarmonyPrefix]
    //    [HarmonyPatch(typeof(BlockTorch), nameof(BlockTorch.OnHeldInteractStart))]
    //    public static bool OnStart(BlockTorch __instance, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    //    {
    //        Traverse t = Traverse.Create(__instance);
    //        bool isExtinct = t.Field<bool>("isExtinct").Value;

    //        if (blockSel != null)
    //        {
    //            IIgnitable ign = byEntity.World.BlockAccessor.GetBlock(blockSel.Position) as IIgnitable;
    //            if (ign != null)
    //            {
    //                EntityPlayer player = byEntity as EntityPlayer;
    //                if (player != null && !byEntity.World.Claims.TryAccess(player.Player, blockSel.Position, EnumBlockAccessFlags.Use))
    //                {
    //                    return false;
    //                }
    //                if (isExtinct)
    //                {
    //                    if (ign.OnTryIgniteStack(byEntity, blockSel.Position, slot, 0f) == EnumIgniteState.Ignitable)
    //                    {
    //                        IWorldAccessor world = byEntity.World;
    //                        AssetLocation location = new AssetLocation("sounds/torch-ignite");
    //                        EntityPlayer entityPlayer = byEntity as EntityPlayer;
    //                        world.PlaySoundAt(location, byEntity, (entityPlayer != null) ? entityPlayer.Player : null, false, 16f, 1f);
    //                        handling = EnumHandHandling.PreventDefault;
    //                        return false;
    //                    }
    //                }
    //                else
    //                {
    //                    handling = EnumHandHandling.Handled;
    //                }
    //                return false;
    //            }
    //        }

    //        return false;
    //    }

    //    public override void Dispose()
    //    {
    //        harmony?.UnpatchAll(_harmonyID);
    //    }
    //}

    class ModSystemCanBeIgnited : ModSystem
    {
        public static ICoreAPI _api;
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockBehaviorClass("CanBeIgnited", typeof(BlockBehaviorCanBeIgnited));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            _api = api;
        }
    }

    class BlockBehaviorCanBeIgnited : BlockBehavior
    {
        public AssetLocation itemToBecome;

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            AssetLocation test = base.collObj.Code;

            string _domain="";
            string _code="";
            _domain = base.collObj.Code.Domain;
            _code = base.collObj.Code.Path;

            if (properties.KeyExists("auto") && properties["auto"].AsBool() == true)
            {
                _code = _code.Replace("extinct", "lit");
            }
            else
            {
                if (properties.KeyExists("domainToBecome"))
                    _domain = properties["domainToBecome"].AsString();
                if (properties.KeyExists("itemToBecome"))
                    _code = properties["itemToBecome"].AsString();
            }

            itemToBecome = new AssetLocation(_domain, _code);

            string thing = "";
            thing += "another thing.";
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling blockHandling)
        {
            if (blockSel == null)
            {
                return;
            }
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            EntityPlayer entityPlayer = byEntity as EntityPlayer;
            IPlayer byPlayer = (entityPlayer != null) ? entityPlayer.Player : null;

            if (!byEntity.Controls.Sneak) 
            {
                return;
            }
            blockHandling = EnumHandling.PreventDefault;
            handHandling = EnumHandHandling.PreventDefault;
            byEntity.World.PlaySoundAt(new AssetLocation("game:sounds/torch-ignite"), byEntity, byPlayer, false, 16f, 1f);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel == null)
            {
                return false;
            }
            EntityPlayer entityPlayer = byEntity as EntityPlayer;
            IPlayer byPlayer = (entityPlayer != null) ? entityPlayer.Player : null;


            handling = EnumHandling.PreventDefault;
            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();
                tf.Translation.Set(0f, Math.Min(0.36666667f, secondsUsed * 4f / 3f) / 2f, -Math.Min(1.1f, secondsUsed * 4f));
                tf.Rotation.X = -Math.Min(30f, secondsUsed * 90f * 2f);
                tf.Rotation.Z = -Math.Min(20f, secondsUsed * 90f * 4f);
                byEntity.Controls.UsingHeldItemTransformBefore = tf;

                if(WillLight(byEntity, blockSel, entitySel))
                {
                    //Smoke particles
                    if (secondsUsed > 0.25f && (int)(30f * secondsUsed) % 2 == 1)
                    {
                        Random rand = byEntity.World.Rand;
                        Vec3d pos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition).Add(rand.NextDouble() * 0.25 - 0.125, rand.NextDouble() * 0.25 - 0.125, rand.NextDouble() * 0.25 - 0.125);
                        Block blockFire = byEntity.World.GetBlock(new AssetLocation("fire"));
                        AdvancedParticleProperties props = blockFire.ParticleProperties[blockFire.ParticleProperties.Length - 1].Clone();
                        props.basePos = pos;
                        props.Quantity.avg = 0.5f;
                        byEntity.World.SpawnParticles(props, byPlayer);
                        props.Quantity.avg = 0f;
                    }
                }
            }

            return byEntity.World.Side == EnumAppSide.Server || (double)secondsUsed <= 2.2;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel == null || secondsUsed < 2f)
            {
                return;
            }
            EntityPlayer entityPlayer = byEntity as EntityPlayer;
            IPlayer byPlayer = (entityPlayer != null) ? entityPlayer.Player : null;

            handling = EnumHandling.PreventDefault;

            if (blockSel != null && byEntity.World.Side == EnumAppSide.Server)
            {
                if (byPlayer.InventoryManager.ActiveHotbarSlot == null)
                    return;

                if (!WillLight(byEntity, blockSel, entitySel))
                    return;

                int previousItemCount = byPlayer.InventoryManager.ActiveHotbarSlot.StackSize;
                byPlayer.InventoryManager.ActiveHotbarSlot.TakeOutWhole();
                Block newBlock = ModSystemCanBeIgnited._api.World.GetBlock(itemToBecome);
                if (newBlock == null)
                    return;
                byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack = new ItemStack( newBlock, previousItemCount);
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
            }
        }

        public bool WillLight(EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            //The selected block is a torch
            Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
            if (block.Code.FirstCodePart() == "torch" && block.FirstCodePart(2) == "lit")
                return true;

            //The seletect block is a torch holder thats filled
            if (block.Code.FirstCodePart() == "torchholder" && block.FirstCodePart(2) == "filled")
                return true;

            //A BlockEntity implementing the IHeatSource interface
            BlockEntity blockEntity = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            IHeatSource entityHeatSource = blockEntity as IHeatSource;
            if (entityHeatSource != null && entityHeatSource.GetHeatStrength(byEntity.World, blockSel.Position, blockSel.Position) > 0)
                return true;

            //A Block with the BlockBehaviorHeatSource behavior 
            IHeatSource heatSource = block.GetBehavior<BlockBehaviorHeatSource>() as IHeatSource;
            if (heatSource != null && heatSource.GetHeatStrength(byEntity.World, blockSel.Position, blockSel.Position) > 0)
                return true;

            //A BlockEntity adjacent to the selected block that is burning   (fire spread entity)
            BlockEntity blockEntityAdjacent = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position.AddCopy(blockSel.Face));
            BEBehaviorBurning beburningBh = (blockEntityAdjacent != null) ? blockEntityAdjacent.GetBehavior<BEBehaviorBurning>() : null;
            if (beburningBh != null && beburningBh.IsBurning)
                return true;

            //Adjacent block is lava
            Block adjacentBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position.AddCopy(blockSel.Face));
            IBlockFlowing lavaBlock = adjacentBlock as IBlockFlowing;
            if (lavaBlock != null && lavaBlock.IsLava)
                return true;

            return false;
        }

        public BlockBehaviorCanBeIgnited(Block block) : base(block) { }
    }
}
