using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EloBuddy;
using LeagueSharp.Common;
using SharpDX;

namespace DZLib.Hero
{
    static class HeroExtensions
    {
        public static List<AIHeroClient> GetLhEnemiesNear(this Vector3 position, float range, float healthpercent)
        {
            return HeroManager.Enemies.Where(hero => hero.LSIsValidTarget(range, true, position) && hero.HealthPercent <= healthpercent).ToList();
        }
    }
}
