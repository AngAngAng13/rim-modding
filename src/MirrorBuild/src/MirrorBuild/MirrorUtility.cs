using System.Collections.Generic;
using Verse;

namespace MirrorBuild
{
    public static class MirrorUtility
    {
        public static IntVec3 MirrorVertical(IntVec3 c, int axisX)
        {
            return new IntVec3(2 * axisX - c.x, c.y, c.z);
        }

        public static IntVec3 MirrorHorizontal(IntVec3 c, int axisZ)
        {
            return new IntVec3(c.x, c.y, 2 * axisZ - c.z);
        }

        public static Rot4 MirrorRotVertical(Rot4 rot)
        {
            return new Rot4((4 - rot.AsInt) % 4);
        }

        public static Rot4 MirrorRotHorizontal(Rot4 rot)
        {
            return new Rot4((rot.AsInt + 2) % 4);
        }

        public static IEnumerable<IntVec3> MirrorCells(IntVec3 c, MirrorMode mode, int axisX, int axisZ)
        {
            if (mode == MirrorMode.Vertical || mode == MirrorMode.FourWay)
                yield return MirrorVertical(c, axisX);
            if (mode == MirrorMode.Horizontal || mode == MirrorMode.FourWay)
                yield return MirrorHorizontal(c, axisZ);
            if (mode == MirrorMode.FourWay)
                yield return MirrorHorizontal(MirrorVertical(c, axisX), axisZ);
        }

        public static IEnumerable<(IntVec3 cell, Rot4 rot)> Mirrors(IntVec3 c, Rot4 rot, MirrorMode mode, int axisX, int axisZ)
        {
            if (mode == MirrorMode.Vertical || mode == MirrorMode.FourWay)
                yield return (MirrorVertical(c, axisX), MirrorRotVertical(rot));
            if (mode == MirrorMode.Horizontal || mode == MirrorMode.FourWay)
                yield return (MirrorHorizontal(c, axisZ), MirrorRotHorizontal(rot));
            if (mode == MirrorMode.FourWay)
                yield return (MirrorHorizontal(MirrorVertical(c, axisX), axisZ), MirrorRotHorizontal(MirrorRotVertical(rot)));
        }
    }
}
