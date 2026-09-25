using System.Collections.Generic;
using CombatExtended.AI;
using Verse;
using Verse.AI;

namespace FirefoamThrower
{
    public class JobDriver_ThrowFirefoam : IJobDriver_Tactical
    {
        private ThingWithComps oldWeapon;

        private ThingWithComps JobWeapon => TargetA.Thing as ThingWithComps;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (pawn.equipment == null)
            {
                yield break;
            }
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDespawnedOrNull(TargetIndex.B);

            yield return Toils_General.Do(EquipJobWeapon);

            yield return Toils_Combat.GotoCastPosition(TargetIndex.B);

            foreach (Toil toil in Toils_CombatCE.AttackStatic(this, TargetIndex.B))
            {
                yield return toil;
            }

            this.AddFinishAction(delegate (JobCondition _)
            {
                RestoreOldWeapon();
            });
        }

        private void EquipJobWeapon()
        {
            ThingOwner stash = Stash();
            if (stash == null)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            if (pawn.equipment.Primary != null)
            {
                oldWeapon = pawn.equipment.Primary;
                ThingDef foamDef = DefDatabase<ThingDef>.GetNamed("CE_Weapon_GrenadeFirefoam", false);
                if (foamDef != null && oldWeapon.def == foamDef)
                {
                    ThingWithComps realGun = BetterWeaponIn(stash, foamDef);
                    if (realGun != null)
                    {
                        oldWeapon = realGun;
                    }
                }
                ThingWithComps stashing = pawn.equipment.Primary;
                bool stashed = pawn.equipment.TryTransferEquipmentToContainer(stashing, stash);
                if (!stashed)
                {
                    oldWeapon = null;
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
            }
            ThingWithComps weapon = JobWeapon;
            if (weapon == null || weapon.Destroyed)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            if (pawn.equipment.Primary != null)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            pawn.equipment.AddEquipment(TakeSingle(weapon));
            job.verbToUse = pawn.equipment.PrimaryEq?.PrimaryVerb;
            if (job.verbToUse == null)
            {
                EndJobWith(JobCondition.Incompletable);
            }
        }

        private void RestoreOldWeapon()
        {
            if (oldWeapon == null || oldWeapon.Destroyed || pawn.equipment == null)
            {
                return;
            }
            if (oldWeapon == pawn.equipment.Primary)
            {
                return;
            }
            if (pawn.equipment.Primary != null)
            {
                ThingOwner stash = Stash();
                if (stash != null && !pawn.equipment.TryTransferEquipmentToContainer(pawn.equipment.Primary, stash))
                {
                    return;
                }
                if (stash == null)
                {
                    pawn.equipment.MakeRoomFor(oldWeapon);
                }
            }
            if (pawn.equipment.Primary != null)
            {
                return;
            }
            pawn.equipment.AddEquipment(TakeSingle(oldWeapon));
        }

        private ThingOwner Stash()
        {
            return CompInventory?.container ?? pawn.inventory?.innerContainer;
        }

        private static ThingWithComps BetterWeaponIn(ThingOwner stash, ThingDef foamDef)
        {
            ThingWithComps meleeFallback = null;
            foreach (Thing thing in stash)
            {
                ThingWithComps weapon = thing as ThingWithComps;
                if (weapon == null || weapon.def == foamDef)
                {
                    continue;
                }
                if (weapon.def.IsRangedWeapon)
                {
                    return weapon;
                }
                if (meleeFallback == null && weapon.def.IsMeleeWeapon)
                {
                    meleeFallback = weapon;
                }
            }
            return meleeFallback;
        }

        private static ThingWithComps TakeSingle(ThingWithComps weapon)
        {
            if (weapon.stackCount > 1)
            {
                return (ThingWithComps)weapon.SplitOff(1);
            }
            weapon.holdingOwner?.Take(weapon, 1);
            return weapon;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref oldWeapon, "oldWeapon");
        }
    }
}
