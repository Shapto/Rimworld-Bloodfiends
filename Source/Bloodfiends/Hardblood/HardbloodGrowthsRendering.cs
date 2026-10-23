using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Bloodfiends.Hardblood
{
    /// <summary>
    /// Hardblood grown over a pawn (a Bloodbag): the crystals on its head and body, and on the weapon it holds.
    /// </summary>
    public class Hediff_HardbloodGrowths : HediffWithComps
    {
        public int familyId = -1;   // the Family this Bloodbag belongs to
        public override void PostRemoved()
        {
            base.PostRemoved();
            HardbloodGraphics.Forget(pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref familyId, "familyId", -1);
        }
    }

    /// <summary>
    /// Draws a Bloodbag's face shadow on the head's mesh, between the face and the eyes.
    /// </summary>
    public class PawnRenderNode_BloodbagFaceShadow : PawnRenderNode
    {
        public PawnRenderNode_BloodbagFaceShadow(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
        {
        }

        public override Graphic GraphicFor(Pawn pawn) => HardbloodGraphics.FaceShadowGraphic(pawn);

        public override GraphicMeshSet MeshSetFor(Pawn pawn) => HumanlikeMeshPoolUtility.GetHumanlikeHeadSetForPawn(pawn);
    }

    /// <summary>
    /// Which part the growths' render node draws: the ones on the head, or the ones on the body.
    /// </summary>
    public class PawnRenderNodeProperties_HardbloodGrowths : PawnRenderNodeProperties
    {
        public bool onHead;
    }

    /// <summary>
    /// Draws a pawn's hardblood growths, on the same mesh as its head or body so they line up with the textures they
    /// were built on. Draws nothing until the growths are built.
    /// </summary>
    public class PawnRenderNode_HardbloodGrowths : PawnRenderNode
    {
        private bool OnHead => ((PawnRenderNodeProperties_HardbloodGrowths)props).onHead;

        public PawnRenderNode_HardbloodGrowths(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
        {
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            return HardbloodGraphics.GrowthGraphic(pawn, OnHead);
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            return OnHead ? HumanlikeMeshPoolUtility.GetHumanlikeHeadSetForPawn(pawn) : HumanlikeMeshPoolUtility.GetHumanlikeBodySetForPawn(pawn);
        }
    }
}
