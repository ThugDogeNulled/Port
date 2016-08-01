﻿using System.Linq;
using EloBuddy;
using LeagueSharp.Common;
using VayneHunter_Reborn.Modules.ModuleHelpers;
using VayneHunter_Reborn.Skills.Condemn;
using VayneHunter_Reborn.Utility;
using VayneHunter_Reborn.Utility.MenuUtility;

namespace VayneHunter_Reborn.Modules.ModuleList.Condemn
{
    class AutoKindred : IModule
    {
        public void OnLoad()
        {

        }

        public bool ShouldGetExecuted()
        {
            return false && MenuExtensions.GetItemValue<bool>("dz191.vhr.misc.condemn.repelkindred") &&
                   Variables.spells[SpellSlot.E].LSIsReady() && ObjectManager.Player.LSCountEnemiesInRange(1500f) == 1 
                   && ObjectManager.Player.LSGetAlliesInRange(1500f).Count(m => !m.IsMe) == 0;
        }

        public ModuleType GetModuleType()
        {
            return ModuleType.OnUpdate;
        }

        public void OnExecute()
        {
            var CondemnTarget =
                HeroManager.Enemies.FirstOrDefault(h => h.LSIsValidTarget(Variables.spells[SpellSlot.E].Range) && h.HasBuff("KindredRNoDeathBuff") &&
                        h.HealthPercent <= 10);
            if (CondemnTarget.LSIsValidTarget())
            {
                Variables.spells[SpellSlot.E].CastOnUnit(CondemnTarget);
            }
        }
    }
}
