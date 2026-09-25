using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace StrongWind
{
    public class SwirlFlyer : Thing, IThingHolder
    {
        private ThingOwner<Thing> innerContainer;

        private Tornado tornado;

        private Vector3 anchor;

        private float angle;

        private float radius = 7f;

        private int ticksLeft = 300;

        private int spinTotalTicks = 300;

        private int ticksSpinning;

        private Vector3 groundPos;

        private Vector3 effectivePos;

        private Vector3 jitter;

        private Vector3 jitterTarget;

        private float baseAltitude;

        private float effectiveHeight;

        private JobQueue jobQueue;

        private int lastCollisionTick = -999;

        private const int CollisionInterval = 15;

        private const int CollisionCooldown = 60;

        private const float CollisionDistance = 1.5f;

        private static readonly AccessTools.FieldRef<Tornado, Vector2> TornadoRealPosition =
            AccessTools.FieldRefAccess<Tornado, Vector2>("realPosition");

        private const float BobAmount = 1f;

        private const float BobPeriod = 20f;

        public Thing Carried
        {
            get
            {
                if (innerContainer.InnerListForReading.Count <= 0)
                {
                    return null;
                }
                return innerContainer.InnerListForReading[0];
            }
        }

        public Pawn Victim => Carried as Pawn;

        public override Vector3 DrawPos => effectivePos;

        public SwirlFlyer()
        {
            innerContainer = new ThingOwner<Thing>(this);
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public static int ActiveCount(Map map)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamed("SwirlFlyer", false);
            if (def == null)
            {
                return 0;
            }
            return map.listerThings.ThingsOfDef(def).Count;
        }

        public static SwirlFlyer MakeSwirl(Thing thing, Tornado tornado)
        {
            SwirlFlyer flyer = (SwirlFlyer)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SwirlFlyer"));
            flyer.tornado = tornado;
            flyer.anchor = thing.TrueCenter();
            flyer.angle = Rand.Range(0f, 360f);
            int min = Mathf.Min(StrongWindMod.Settings.spinDurationMinTicks, StrongWindMod.Settings.spinDurationMaxTicks);
            int max = Mathf.Max(StrongWindMod.Settings.spinDurationMinTicks, StrongWindMod.Settings.spinDurationMaxTicks);
            flyer.spinTotalTicks = Rand.RangeInclusive(min, max);
            flyer.ticksLeft = flyer.spinTotalTicks;
            flyer.baseAltitude = thing.DrawPos.y;
            flyer.ticksSpinning = 0;
            flyer.radius = StrongWindMod.Settings.startRadius;
            Pawn pawn = thing as Pawn;
            if (pawn != null)
            {
                if (pawn.CurJob != null)
                {
                    pawn.jobs.SuspendCurrentJob(JobCondition.InterruptForced);
                }
                flyer.jobQueue = pawn.jobs.CaptureAndClearJobQueue();
            }
            if (thing.Spawned)
            {
                thing.DeSpawn(DestroyMode.WillReplace);
            }
            if (!flyer.innerContainer.TryAdd(thing))
            {
                Log.Error("Could not add " + thing.ToStringSafe() + " to a swirl flyer.");
                thing.Destroy();
                return null;
            }
            return flyer;
        }

        protected override void Tick()
        {
            base.Tick();
            Thing carried = Carried;
            if (carried == null)
            {
                Destroy();
                return;
            }
            if (tornado != null && tornado.Spawned)
            {
                Vector2 funnel = TornadoRealPosition(tornado);
                anchor = new Vector3(funnel.x, 0f, funnel.y);
            }
            StrongWindSettings s = StrongWindMod.Settings;
            angle += s.angularSpeed;
            ticksSpinning++;
            ticksLeft--;
            float progress = (float)ticksSpinning / Mathf.Max(spinTotalTicks, 1);
            radius = Mathf.Lerp(s.startRadius, s.endRadius, Mathf.Min(progress, 1f));
            Vector3 center = anchor + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * radius, 0f, Mathf.Sin(angle * Mathf.Deg2Rad) * radius);
            IntVec3 cell = center.ToIntVec3();
            if (!cell.InBounds(base.Map))
            {
                Eject();
                return;
            }
            groundPos = new Vector3(center.x, baseAltitude, center.z);
            base.Position = cell;
            effectiveHeight = Mathf.Lerp(s.startHeight, s.maxHeight, Mathf.Min(progress, 1f)) + Mathf.Sin((float)ticksSpinning / BobPeriod) * BobAmount;
            if (this.IsHashIntervalTick(20))
            {
                jitterTarget = Vector3Utility.RandomHorizontalOffset(0.6f);
            }
            jitter += (jitterTarget - jitter) * 0.2f;
            Vector3 pos = groundPos + Altitudes.AltIncVect * effectiveHeight;
            pos += Vector3.forward * (def.pawnFlyer.heightFactor * effectiveHeight);
            effectivePos = pos + jitter;
            if (carried is Pawn carriedPawn)
            {
                carriedPawn.Rotation = Rot4.FromAngleFlat(angle + 90f);
            }
            if (s.collisionsEnabled && this.IsHashIntervalTick(CollisionInterval))
            {
                CheckCollision();
            }
            if (ticksLeft <= 0 || (tornado != null && !tornado.Spawned && ticksLeft < spinTotalTicks - 60))
            {
                Eject();
            }
        }

        private void CheckCollision()
        {
            Thing mine = Carried;
            if (mine == null || Find.TickManager.TicksGame - lastCollisionTick < CollisionCooldown)
            {
                return;
            }
            foreach (Thing t in base.Map.listerThings.ThingsOfDef(def))
            {
                if (t == this || !(t is SwirlFlyer other))
                {
                    continue;
                }
                Thing theirs = other.Carried;
                if (theirs == null || Find.TickManager.TicksGame - other.lastCollisionTick < CollisionCooldown)
                {
                    continue;
                }
                if ((other.groundPos - groundPos).MagnitudeHorizontalSquared() > CollisionDistance * CollisionDistance)
                {
                    continue;
                }
                Pawn myPawn = mine as Pawn;
                Pawn theirPawn = theirs as Pawn;
                bool mineIsItem = myPawn == null;
                bool theirsIsItem = theirPawn == null;
                if (mineIsItem && theirsIsItem)
                {
                    continue;
                }
                int now = Find.TickManager.TicksGame;
                if (!mineIsItem && !theirsIsItem)
                {
                    if (!Rand.Chance(0.35f))
                    {
                        continue;
                    }
                    HurtPawn(myPawn, Rand.RangeInclusive(2, 5));
                    HurtPawn(theirPawn, Rand.RangeInclusive(2, 5));
                }
                else if (!mineIsItem)
                {
                    if (!Rand.Chance(0.5f))
                    {
                        continue;
                    }
                    HurtPawn(myPawn, Rand.RangeInclusive(8, 16));
                }
                else
                {
                    if (!Rand.Chance(0.5f))
                    {
                        continue;
                    }
                    HurtPawn(theirPawn, Rand.RangeInclusive(8, 16));
                }
                lastCollisionTick = now;
                other.lastCollisionTick = now;
                FleckMaker.ThrowDustPuff((groundPos + other.groundPos) / 2f + Altitudes.AltIncVect * effectiveHeight, base.Map, 1f);
                return;
            }
        }

        private static void HurtPawn(Pawn pawn, int amount)
        {
            int scaled = GenMath.RoundRandom(amount * StrongWindMod.Settings.collisionDamageScale);
            if (scaled > 0)
            {
                pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, scaled, 0f, -1f, null));
            }
        }

        private void Eject()
        {
            Thing carried = Carried;
            if (carried == null)
            {
                Destroy();
                return;
            }
            IntVec3 cell = groundPos.ToIntVec3();
            if (!cell.InBounds(base.Map))
            {
                cell = base.Position;
            }
            innerContainer.TryDrop(carried, cell, base.Map, ThingPlaceMode.Near, out _);
            Pawn victim = carried as Pawn;
            if (victim == null)
            {
                FleckMaker.ThrowDustPuff(carried.TrueCenter() + Gen.RandomHorizontalVector(0.5f), base.Map, 1f);
                Destroy();
                return;
            }
            victim.Rotation = Rot4.Random;
            if (jobQueue != null)
            {
                victim.jobs.RestoreCapturedJobs(jobQueue);
            }
            victim.jobs.CheckForJobOverride(0f, ignoreQueue: false);
            victim.stances.stunner.StunFor(Rand.RangeInclusive(StrongWindMod.Settings.stunMinTicks, StrongWindMod.Settings.stunMaxTicks), null, addBattleLog: false, showMote: false);
            FleckMaker.ThrowDustPuff(victim.TrueCenter() + Gen.RandomHorizontalVector(0.5f), base.Map, 2f);
            Destroy();
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            Thing carried = Carried;
            if (carried is Pawn)
            {
                carried.DynamicDrawPhaseAt(phase, effectivePos);
            }
            else if (carried != null && phase == DrawPhase.Draw)
            {
                carried.DrawNowAt(effectivePos, false);
            }
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Material shadowMaterial = def.pawnFlyer.ShadowMaterial;
            if (shadowMaterial != null)
            {
                float num = Mathf.Lerp(1f, 0.6f, effectiveHeight);
                Vector3 s = new Vector3(num, 1f, num);
                Matrix4x4 matrix = default(Matrix4x4);
                matrix.SetTRS(groundPos, Quaternion.identity, s);
                Graphics.DrawMesh(MeshPool.plane10, matrix, shadowMaterial, 0);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref anchor, "anchor");
            Scribe_Values.Look(ref angle, "angle", 0f);
            Scribe_Values.Look(ref radius, "radius", 7f);
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
            Scribe_Values.Look(ref spinTotalTicks, "spinTotalTicks", 300);
            Scribe_Values.Look(ref baseAltitude, "baseAltitude", 0f);
            Scribe_Values.Look(ref ticksSpinning, "ticksSpinning", 0);
            Scribe_Values.Look(ref groundPos, "groundPos");
            Scribe_References.Look(ref tornado, "tornado");
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Deep.Look(ref jobQueue, "jobQueue");
        }
    }
}
