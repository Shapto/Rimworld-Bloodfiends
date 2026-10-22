using SingularityFramework.Kitbash;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Bloodfiends.Hardblood
{
    public class HardbloodMask
    {
        private const float AngleMinimum = -100f;
        private const float AngleMaximum = 110f;
        private const float Rise = 0.06f;               // how far above the top of the head the mask itself reaches
        private const float BigPieceShare = 0.55f;      // a big crystal is this share of the head's width
        private const float Standoff = 1.08f;           // the mask sits a little proud of the face
        private const float StandsUpFrom = 0.5f;        // above halfway up the head, the mask rises straight up

        public readonly SurfaceSpace face;
        public readonly KitbashCanvas flat;

        public HardbloodMask(PixelMask southHead, List<KitbashPiece> pieces, int seed, int scale)
        {
            face = new SurfaceSpace(southHead, AngleMinimum, AngleMaximum);
            int width = face.width;
            int height = face.height;
            var target = new PixelMask(width, height);
            var pointsRoom = new PixelMask(width, height);
            var outlineToKeep = new PixelMask(width, height);

            for (int row = 0; row < height; row++)
            {
                float down = face.ShareDown(row);
                float guide = 4f + 10f * (down - 0.5f);
                float outerEdge = 50f + 42f * Mathf.Exp(-Mathf.Pow((down - 0.55f) / 0.38f, 2f));
                bool hasHead = face.radiusByRow[row] > face.referenceRadius * 0.15f;
                bool lowerFace = down >= 0.5f && hasHead;
                bool upperFace = down < 0.5f && down >= -Rise;
                for (int column = 0; column < width; column++)
                {
                    float angle = face.AngleForColumn(column);
                    int index = row * width + column;
                    bool side = angle >= guide && angle <= outerEdge;
                    target.cells[index] = side && (lowerFace || upperFace);
                    pointsRoom.cells[index] = down < 0.35f && down > -Rise - 0.5f && angle >= guide && angle <= outerEdge + 10f;
                    outlineToKeep.cells[index] = Mathf.Abs(angle - guide) >= 6f;
                }
            }

            PixelMask allowed = target.Dilated((int)(face.referenceRadius * 0.1f)).Or(pointsRoom);
            PixelMask outline = target.AndNot(target.Eroded(2));
            var assembly = new KitbashAssembly(target, allowed, pieces, new KitbashRandom(seed), face.referenceRadius * 2f * BigPieceShare, scale)
            {
                crownStyleSeed = seed,
                outlineToFollow = outline.And(outlineToKeep).Dilated(4 * scale)
            };
            assembly.Build();
            flat = assembly.Render();
        }

        public KitbashCanvas Facing(PixelMask viewHead, int rotation)
        {
            return face.WrapFlat(flat, viewHead, rotation, Standoff, StandsUpFrom);
        }
    }
}
