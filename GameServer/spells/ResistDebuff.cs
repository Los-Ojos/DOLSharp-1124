/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using System;
using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.Language;
using System.Collections.Generic;

namespace DOL.GS.Spells
{
    /// <summary>
    /// Base class for all resist debuffs, needed to set effectiveness and duration
    /// </summary>
    public abstract class AbstractResistDebuff : PropertyChangingSpell
    {
        /// <summary>
        /// Gets debuff type name for delve info
        /// </summary>
        public abstract string DebuffTypeName { get; }

        /// <summary>
        /// Debuff category is 3 for debuffs
        /// </summary>
        public override eBuffBonusCategory BonusCategory1 => eBuffBonusCategory.Debuff;

        /// <summary>
        /// Calculates the effect duration in milliseconds
        /// </summary>
        /// <param name="target">The effect target</param>
        /// <param name="effectiveness">The effect effectiveness</param>
        /// <returns>The effect duration in milliseconds</returns>
        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            double duration = Spell.Duration;

            duration *= 1.0 + Caster.GetModified(eProperty.SpellDuration) * 0.01;
            duration -= duration * target.GetResist(Spell.DamageType) * 0.01;

            if (duration < 1)
            {
                duration = 1;
            }
            else if (duration > (Spell.Duration * 4))
            {
                duration = Spell.Duration * 4;
            }

            return (int)duration;
        }

        /// <summary>
        /// Apply effect on target or do spell action if non duration spell
        /// </summary>
        /// <param name="target">target that gets the effect</param>
        /// <param name="effectiveness">factor from 0..1 (0%-100%)</param>
        public override void ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            // TODO: correct effectiveness formula
            // invoke direct effect if not resisted for DD w/ debuff spells
            if (Caster is GamePlayer player && Spell.Level > 0)
            {
                if (player.CharacterClass.ClassType == eClassType.ListCaster)
                {
                    int specLevel = Caster.GetModifiedSpecLevel(SpellLine.Spec);
                    effectiveness = 0.75;
                    effectiveness += (specLevel - 1.0) * 0.5 / Spell.Level;
                    effectiveness = Math.Max(0.75, effectiveness);
                    effectiveness = Math.Min(1.25, effectiveness);
                    effectiveness *= 1.0 + Caster.GetModified(eProperty.BuffEffectiveness) * 0.01;
                }
                else
                    {
                        effectiveness = 1.0;
                        effectiveness *= 1.0 + Caster.GetModified(eProperty.DebuffEffectivness) * 0.01;
                    }
            }

            base.ApplyEffectOnTarget(target, effectiveness);

            if (target.Realm == 0 || Caster.Realm == 0)
            {
                target.LastAttackedByEnemyTickPvE = target.CurrentRegion.Time;
                Caster.LastAttackTickPvE = Caster.CurrentRegion.Time;
            }
            else
            {
                target.LastAttackedByEnemyTickPvP = target.CurrentRegion.Time;
                Caster.LastAttackTickPvP = Caster.CurrentRegion.Time;
            }

            if (target is GameNPC npc)
            {
                if (npc.Brain is IOldAggressiveBrain aggroBrain)
                {
                    aggroBrain.AddToAggroList(Caster, 1);
                }
            }

            if (Spell.CastTime > 0)
            {
                target.StartInterruptTimer(target.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
            }
        }

        /// <summary>
        /// Calculates chance of spell getting resisted
        /// </summary>
        /// <param name="target">the target of the spell</param>
        /// <returns>chance that spell will be resisted for specific target</returns>
        public override int CalculateSpellResistChance(GameLiving target)
        {
            int basechance = base.CalculateSpellResistChance(target);
            GameSpellEffect rampage = FindEffectOnTarget(target, "Rampage");
            if (rampage != null)
            {
                basechance += (int)rampage.Spell.Value;
            }

            return Math.Min(100, basechance);
        }

        /// <summary>
        /// Updates changes properties to living
        /// </summary>
        /// <param name="target"></param>
        protected override void SendUpdates(GameLiving target)
        {
            base.SendUpdates(target);
            if (target is GamePlayer)
            {
                GamePlayer player = (GamePlayer)target;
                player.Out.SendCharResistsUpdate();
            }
        }

        /// <summary>
        /// Delve Info
        /// </summary>
        public override IList<string> DelveInfo
        {
            get
            {
                /*
                <Begin Info: Nullify Dissipation>
                Function: resistance decrease

                Decreases the target's resistance to the listed damage type.

                Resist decrease Energy: 15
                Target: Targetted
                Range: 1500
                Duration: 15 sec
                Power cost: 13
                Casting time:      2.0 sec
                Damage: Cold

                <End Info>
                 */

                var list = new List<string>();
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "ResistDebuff.DelveInfo.Function"));
                list.Add(" "); // empty line
                list.Add(Spell.Description);
                list.Add(" "); // empty line
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "ResistDebuff.DelveInfo.Decrease", DebuffTypeName, Spell.Value));
                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.Target", Spell.Target));
                if (Spell.Range != 0)
                {
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.Range", Spell.Range));
                }

                if (Spell.Duration >= ushort.MaxValue * 1000)
                {
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.Duration") + " Permanent.");
                }
                else if (Spell.Duration > 60000)
                {
                    list.Add($"{LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.Duration")}{Spell.Duration / 60000}:{Spell.Duration % 60000 / 1000:00} min");
                }
                else if (Spell.Duration != 0)
                {
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.Duration") + (Spell.Duration / 1000).ToString("0' sec';'Permanent.';'Permanent.'"));
                }

                if (Spell.Power != 0)
                {
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.PowerCost", Spell.Power.ToString("0;0'%'")));
                }

                list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.CastingTime", (Spell.CastTime * 0.001).ToString("0.0## sec;-0.0## sec;'instant'")));
                if (Spell.RecastDelay > 60000)
                {
                    list.Add($"{LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.RecastTime")}{Spell.RecastDelay / 60000}:{Spell.RecastDelay % 60000 / 1000:00} min");
                }
                else if (Spell.RecastDelay > 0)
                {
                    list.Add($"{LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.RecastTime")}{Spell.RecastDelay / 1000} sec");
                }

                if (Spell.Concentration != 0)
                {
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.ConcentrationCost", Spell.Concentration));
                }

                if (Spell.Radius != 0)
                {
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.Radius", Spell.Radius));
                }

                if (Spell.DamageType != eDamageType.Natural)
                {
                    list.Add(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "DelveInfo.Damage", GlobalConstants.DamageTypeToName(Spell.DamageType)));
                }

                return list;
            }
        }

        // constructor
        public AbstractResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    /// <summary>
    /// Body resistance debuff
    /// </summary>
    [SpellHandler("BodyResistDebuff")]
    public class BodyResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 => eProperty.Resist_Body;

        public override string DebuffTypeName => "Body";

        // constructor
        public BodyResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    /// <summary>
    /// Cold resistance debuff
    /// </summary>
    [SpellHandler("ColdResistDebuff")]
    public class ColdResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 => eProperty.Resist_Cold;

        public override string DebuffTypeName => "Cold";

        // constructor
        public ColdResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    /// <summary>
    /// Energy resistance debuff
    /// </summary>
    [SpellHandler("EnergyResistDebuff")]
    public class EnergyResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 => eProperty.Resist_Energy;

        public override string DebuffTypeName => "Energy";

        // constructor
        public EnergyResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    /// <summary>
    /// Heat resistance debuff
    /// </summary>
    [SpellHandler("HeatResistDebuff")]
    public class HeatResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 => eProperty.Resist_Heat;

        public override string DebuffTypeName => "Heat";

        // constructor
        public HeatResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    /// <summary>
    /// Matter resistance debuff
    /// </summary>
    [SpellHandler("MatterResistDebuff")]
    public class MatterResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 => eProperty.Resist_Matter;

        public override string DebuffTypeName => "Matter";

        // constructor
        public MatterResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    /// <summary>
    /// Spirit resistance debuff
    /// </summary>
    [SpellHandler("SpiritResistDebuff")]
    public class SpiritResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 => eProperty.Resist_Spirit;

        public override string DebuffTypeName => "Spirit";

        // constructor
        public SpiritResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    /// <summary>
    /// Slash resistance debuff
    /// </summary>
    [SpellHandler("SlashResistDebuff")]
    public class SlashResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 => eProperty.Resist_Slash;

        public override string DebuffTypeName => "Slash";

        // constructor
        public SlashResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    /// <summary>
    /// Thrust resistance debuff
    /// </summary>
    [SpellHandler("ThrustResistDebuff")]
    public class ThrustResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 => eProperty.Resist_Thrust;

        public override string DebuffTypeName => "Thrust";

        // constructor
        public ThrustResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    /// <summary>
    /// Crush resistance debuff
    /// </summary>
    [SpellHandler("CrushResistDebuff")]
    public class CrushResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 => eProperty.Resist_Crush;

        public override string DebuffTypeName => "Crush";

        // constructor
        public CrushResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    /// <summary>
    /// Crush/Slash/Thrust resistance debuff
    /// </summary>
    [SpellHandler("CrushSlashThrustDebuff")]
    public class CrushSlashThrustDebuff : AbstractResistDebuff
    {
        public override eBuffBonusCategory BonusCategory1 => eBuffBonusCategory.Debuff;

        public override eBuffBonusCategory BonusCategory2 => eBuffBonusCategory.Debuff;

        public override eBuffBonusCategory BonusCategory3 => eBuffBonusCategory.Debuff;

        public override eProperty Property1 => eProperty.Resist_Crush;

        public override eProperty Property2 => eProperty.Resist_Slash;

        public override eProperty Property3 => eProperty.Resist_Thrust;

        public override string DebuffTypeName => "Crush/Slash/Thrust";

        // constructor
        public CrushSlashThrustDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    [SpellHandler("EssenceSear")]
    public class EssenceResistDebuff : AbstractResistDebuff
    {
        public override eProperty Property1 => eProperty.Resist_Natural;

        public override string DebuffTypeName => "Essence";

        // constructor
        public EssenceResistDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }
}
