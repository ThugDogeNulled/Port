﻿using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using Color = System.Drawing.Color;
using EloBuddy;
using LeagueSharp.Common;
using SPrediction;
using SharpDX;
using UnderratedAIO.Helpers;
using Environment = UnderratedAIO.Helpers.Environment;
using Orbwalking = UnderratedAIO.Helpers.Orbwalking;
using Prediction = LeagueSharp.Common.Prediction;
using Utility = LeagueSharp.Common.Utility;

namespace UnderratedAIO.Champions
{
    internal class Veigar
    {
        public static Menu config;
        public static Orbwalking.Orbwalker orbwalker;
        public static AutoLeveler autoLeveler;
        public static Spell Q, W, E, R;
        public static readonly AIHeroClient player = ObjectManager.Player;
        public static bool justQ, justW, justR, justE, Estun;
        public static Vector3 wPos, ePos;
        public static float wTime;
        public Obj_AI_Base qMiniForWait;
        public Obj_AI_Base qMiniTarget;
        public AIHeroClient IgniteTarget;

        public Veigar()
        {
            InitVeigar();
            InitMenu();
            //Game.PrintChat("<font color='#9933FF'>Soresu </font><font color='#FFFFFF'>- Veigar</font>");
            Drawing.OnDraw += Game_OnDraw;
            Game.OnUpdate += Game_OnGameUpdate;
            Obj_AI_Base.OnProcessSpellCast += Game_ProcessSpell;
            Helpers.Jungle.setSmiteSlot();
            Interrupter2.OnInterruptableTarget += Interrupter2_OnInterruptableTarget;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            CustomEvents.Unit.OnDash += Unit_OnDash;

            HpBarDamageIndicator.DamageToUnit = ComboDamage;
        }

        private void Unit_OnDash(Obj_AI_Base sender, Dash.DashItem args)
        {
            if (sender.IsEnemy && sender is AIHeroClient && config.Item("OnDash", true).GetValue<bool>() && E.LSIsReady() &&
                args.EndPos.LSDistance(player.Position) < E.Range)
            {
                CastE((AIHeroClient) sender);
            }
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (config.Item("GapCloser", true).GetValue<bool>() && E.LSIsReady() &&
                gapcloser.End.LSDistance(player.Position) < E.Range)
            {
                CastE(gapcloser.Sender);
            }
        }

        private void Interrupter2_OnInterruptableTarget(AIHeroClient sender,
            Interrupter2.InterruptableTargetEventArgs args)
        {
            if (E.LSIsReady() && config.Item("Interrupt", true).GetValue<bool>() && sender.LSDistance(player) < E.Range)
            {
                CastE(sender);
            }
        }


        private void Game_ProcessSpell(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe)
            {
                if (args.SData.Name == "VeigarBalefulStrike")
                {
                    if (!justQ)
                    {
                        justQ = true;
                        Utility.DelayAction.Add(
                            (int) (player.Position.LSDistance(args.End) / Q.Speed + Q.Delay), () => justQ = false);
                    }
                }
                if (args.SData.Name == "VeigarDarkMatter")
                {
                    if (!justW)
                    {
                        wPos = args.End;
                        justW = true;
                        Utility.DelayAction.Add(
                            1250, () =>
                            {
                                justW = false;
                                wPos = Vector3.Zero;
                                wTime = System.Environment.TickCount;
                            });
                    }
                }
                if (args.SData.Name == "VeigarEventHorizon")
                {
                    if (!justE)
                    {
                        ePos = args.End;
                        justE = true;
                        Estun = true;
                        Utility.DelayAction.Add(
                            3500, () =>
                            {
                                justE = false;
                                ePos = Vector3.Zero;
                            });
                        Utility.DelayAction.Add(700, () => { Estun = false; });
                    }
                }
                if (args.SData.Name == "VeigarPrimordialBurst")
                {
                    if (!justR)
                    {
                        justR = true;
                        Utility.DelayAction.Add(400, () => justR = false);
                    }
                }
            }
        }

        private void InitVeigar()
        {
            Q = new Spell(SpellSlot.Q, 900);
            Q.SetSkillshot(0.25f, 70f, 2000f, false, SkillshotType.SkillshotLine);
            W = new Spell(SpellSlot.W, 900);
            W.SetSkillshot(1.25f, 225f, float.MaxValue, false, SkillshotType.SkillshotCircle);
            E = new Spell(SpellSlot.E, 1050);
            E.SetSkillshot(1.2f, 25f, float.MaxValue, false, SkillshotType.SkillshotCircle);
            R = new Spell(SpellSlot.R, 615);
        }

        private void Game_OnGameUpdate(EventArgs args)
        {
            if (FpsBalancer.CheckCounter())
            {
                return;
            }
            Orbwalking.Attack = true;
            switch (orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    AIHeroClient target = TargetSelector.GetTarget(
                        1000, TargetSelector.DamageType.Magical, true, HeroManager.Enemies.Where(h => h.IsInvulnerable));
                    if (target != null)
                    {
                        var cmbDmg = ComboDamage(target);
                        bool canKill = cmbDmg > target.Health;
                        if (config.Item("usee", true).GetValue<bool>() &&
                            NavMesh.GetCollisionFlags(player.Position).HasFlag(CollisionFlags.Grass) && E.LSIsReady() &&
                            ((canKill && config.Item("useekill", true).GetValue<bool>()) ||
                             (!config.Item("useekill", true).GetValue<bool>() && CheckMana())))
                        {
                            Orbwalking.Attack = false;
                            Combo(target, cmbDmg, canKill, true);
                        }
                        else if (config.Item("startWithE", true).GetValue<bool>() && E.LSIsReady() &&
                                 (!config.Item("checkmana", true).GetValue<bool>() ||
                                  (config.Item("checkmana", true).GetValue<bool>() && CheckMana())))
                        {
                            Combo(target, cmbDmg, canKill, true);
                        }
                        else
                        {
                            Combo(target, cmbDmg, canKill, false);
                        }
                    }

                    break;
                case Orbwalking.OrbwalkingMode.Mixed:
                    Harass();
                    break;
                case Orbwalking.OrbwalkingMode.LaneClear:
                    Clear();
                    break;
                case Orbwalking.OrbwalkingMode.LastHit:
                    Lasthit();
                    break;
                default:
                    break;
            }
            if (config.Item("autoQ", true).GetValue<bool>() && Q.LSIsReady() && !player.LSIsRecalling() &&
                orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.Combo)
            {
                LastHitQ(true);
            }
            AIHeroClient targ = null;
            if (config.Item("autoW", true).GetValue<bool>() || config.Item("autoE", true).GetValue<bool>())
            {
                targ =
                    HeroManager.Enemies.Where(
                        hero =>
                            W.CanCast(hero) &&
                            (hero.HasBuffOfType(BuffType.Snare) || hero.HasBuffOfType(BuffType.Stun) ||
                             hero.HasBuffOfType(BuffType.Taunt) || hero.HasBuffOfType(BuffType.Suppression)))
                        .OrderByDescending(hero => TargetSelector.GetPriority(hero))
                        .ThenBy(hero => hero.Health)
                        .FirstOrDefault();
            }
            if (config.Item("autoW", true).GetValue<bool>() && targ != null && W.LSIsReady() && !player.LSIsRecalling())
            {
                if ((((justQ && targ.Health > Q.GetDamage(targ) || targ.LSCountEnemiesInRange(W.Width) > 1)) || !justQ))
                {
                    W.Cast(targ);
                }
            }
            if (config.Item("autoE", true).GetValue<bool>() && targ != null && E.LSIsReady() && !player.LSIsRecalling())
            {
                CastE(targ);
            }
            if (config.Item("useEkey", true).GetValue<KeyBind>().Active && E.LSIsReady())
            {
                AIHeroClient target = TargetSelector.GetTarget(
                    1000, TargetSelector.DamageType.Magical, true, HeroManager.Enemies.Where(h => h.IsInvulnerable));
                if (target != null)
                {
                    Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
                    switch (config.Item("eType", true).GetValue<StringList>().SelectedIndex)
                    {
                        case 0:
                            CastE(target);
                            break;
                        case 1:
                            CastE(target, false);
                            break;
                    }
                }
            }
            if (Q.LSIsReady() && config.Item("ksQ", true).GetValue<bool>())
            {
                var enemyQ =
                    HeroManager.Enemies.Where(e => e.Health < Q.GetDamage(e) && e.LSIsValidTarget() && Q.CanCast(e))
                        .OrderByDescending(e => TargetSelector.GetPriority(e))
                        .FirstOrDefault();
                if (enemyQ != null)
                {
                    CastQHero(enemyQ);
                }
            }
            if (R.LSIsReady() && config.Item("ksR", true).GetValue<bool>())
            {
                var enemyR =
                    HeroManager.Enemies.Where(
                        e =>
                            R.CanCast(e) && e.Health < R.GetDamage(e) && e.LSIsValidTarget() &&
                            (!e.HasBuff("summonerdot") ||
                             (e.HasBuff("summonerdot") &&
                              (!e.GetBuff("summonerdot").Caster.IsMe ||
                               (e.GetBuff("summonerdot").Caster.IsMe && e.LSCountAlliesInRange(600) > 0)))))
                        .OrderByDescending(e => TargetSelector.GetPriority(e))
                        .FirstOrDefault();
                if (enemyR != null && CheckUltBlock(enemyR))
                {
                    if (enemyR.LSCountEnemiesInRange(2000) == 1)
                    {
                        R.CastOnUnit(enemyR);
                    }
                    else if (!config.Item("ult" + enemyR.BaseSkinName, true).GetValue<bool>())
                    {
                        R.CastOnUnit(enemyR);
                    }
                }
            }
        }

        private bool CheckUltBlock(AIHeroClient enemyR)
        {
            return (!config.Item("ult" + enemyR.BaseSkinName, true).GetValue<bool>() ||
                    player.LSCountEnemiesInRange(1500) == 1);
        }

        private void Harass()
        {
            AIHeroClient target = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);
            float perc = config.Item("minmanaH", true).GetValue<Slider>().Value / 100f;
            if (config.Item("useqLHinHarass", true).GetValue<bool>())
            {
                Lasthit();
            }
            if (player.Mana < player.MaxMana * perc || target == null)
            {
                return;
            }
            if (config.Item("useqH", true).GetValue<bool>() && Q.LSIsReady())
            {
                CastQHero(target);
            }
            if (config.Item("usewH", true).GetValue<bool>() && W.LSIsReady())
            {
                var tarPered = W.GetPrediction(target);
                if (W.Range - 80 > tarPered.CastPosition.LSDistance(player.Position) &&
                    tarPered.Hitchance >= HitChance.VeryHigh)
                {
                    W.Cast(tarPered.CastPosition);
                }
            }
        }

        private void Clear()
        {
            float perc = config.Item("minmana", true).GetValue<Slider>().Value / 100f;
            Lasthit();
            if (player.Mana < player.MaxMana * perc)
            {
                return;
            }
            if (config.Item("usewLC", true).GetValue<bool>() && W.LSIsReady())
            {
                MinionManager.FarmLocation bestPositionW =
                    W.GetCircularFarmLocation(MinionManager.GetMinions(W.Range, MinionTypes.All, MinionTeam.NotAlly));
                if (bestPositionW.MinionsHit >= config.Item("wMinHit", true).GetValue<Slider>().Value)
                {
                    W.Cast(bestPositionW.Position);
                }
            }
        }

        private void Lasthit()
        {
            float perc = config.Item("minmanaLH", true).GetValue<Slider>().Value / 100f;
            if (player.Mana < player.MaxMana * perc)
            {
                return;
            }
            LastHitQ();
        }

        private void Combo(AIHeroClient target, float cmbDmg, bool canKill, bool bush)
        {
            if (target == null || !target.LSIsValidTarget())
            {
                return;
            }
            if (config.Item("useItems").GetValue<bool>())
            {
                ItemHandler.UseItems(target, config, cmbDmg);
            }
            if (config.Item("usew", true).GetValue<bool>() && W.LSIsReady() && W.CanCast(target))
            {
                var tarPered = W.GetPrediction(target);
                if (justE && !Estun && ePos.LSIsValid() && target.LSDistance(ePos) < 375)
                {
                    if (W.Range - 80 > tarPered.CastPosition.LSDistance(player.Position) &&
                        tarPered.Hitchance >= HitChance.High)
                    {
                        W.Cast(target.Position);
                    }
                }
                else
                {
                    if (W.Range - 80 > tarPered.CastPosition.LSDistance(player.Position) &&
                        tarPered.Hitchance >= HitChance.VeryHigh && !config.Item("startWithE", true).GetValue<bool>())
                    {
                        W.Cast(tarPered.CastPosition);
                    }
                }
            }
            if (config.Item("usee", true).GetValue<bool>() && E.LSIsReady() &&
                (((canKill && config.Item("useekill", true).GetValue<bool>()) ||
                  (!config.Item("useekill", true).GetValue<bool>() && CheckMana())) ||
                 config.Item("startWithE", true).GetValue<bool>()))
            {
                switch (config.Item("eType", true).GetValue<StringList>().SelectedIndex)
                {
                    case 0:
                        CastE(target);
                        return;
                    case 1:
                        CastE(target, false);
                        return;
                        break;
                }
            }
            if (config.Item("usee", true).GetValue<bool>() && E.LSIsReady())
            {
                if (config.Item("useemin", true).GetValue<Slider>().Value > 1 &&
                    player.LSCountEnemiesInRange(E.Range + 175) >= config.Item("useemin", true).GetValue<Slider>().Value)
                {
                    switch (config.Item("eType", true).GetValue<StringList>().SelectedIndex)
                    {
                        case 0:
                            CastE(target, true, config.Item("useemin", true).GetValue<Slider>().Value);
                            return;
                    }
                }
            }
            if (config.Item("useq", true).GetValue<bool>() && Q.LSIsReady() && Q.CanCast(target) && target.LSIsValidTarget() &&
                !bush && !Estun)
            {
                CastQHero(target);
            }
            var ignitedmg = (float) player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);
            bool hasIgnite = player.Spellbook.CanUseSpell(player.LSGetSpellSlot("SummonerDot")) == SpellState.Ready;
            if (config.Item("useIgnite", true).GetValue<bool>() &&
                ((ignitedmg > target.Health && hasIgnite && !player.IsChannelingImportantSpell() && !justQ &&
                  !Q.CanCast(target) && !justR && !R.CanCast(target) && CheckW(target)) || IgniteTarget != null))
            {
                if (IgniteTarget != null)
                {
                    player.Spellbook.CastSpell(player.LSGetSpellSlot("SummonerDot"), IgniteTarget);
                    return;
                }
                player.Spellbook.CastSpell(player.LSGetSpellSlot("SummonerDot"), target);
            }
            var castR = false;
            if (target.LSCountEnemiesInRange(2000) == 1)
            {
                castR = true;
            }
            else if (!config.Item("ult" + target.BaseSkinName, true).GetValue<bool>())
            {
                castR = true;
            }
            if (R.LSIsReady() && R.CanCast(target) && CheckUltBlock(target) && config.Item("user", true).GetValue<bool>() &&
                castR && R.ManaCost < player.Mana &&
                !target.Buffs.Any(b => CombatHelper.invulnerable.Contains(b.Name)) &&
                !CombatHelper.CheckCriticalBuffs(target))
            {
                if (config.Item("userPred", true).GetValue<bool>())
                {
                    var Whit = wPos.LSIsValid() && System.Environment.TickCount - wTime > 700 &&
                               Prediction.GetPrediction(target, 0.55f).UnitPosition.LSDistance(wPos) < W.Width;
                    var targetHP = target.Health -
                                   Program.IncDamages.GetEnemyData(target.NetworkId).ProjectileDamageTaken;
                    var killWithIgnite = hasIgnite && config.Item("useIgnite", true).GetValue<bool>() &&
                                         R.GetDamage(target) + ignitedmg > targetHP && targetHP > R.GetDamage(target);

                    var killWithW = wPos != null && Whit && R.GetDamage(target) + W.GetDamage(target) > targetHP &&
                                    target.Health > R.GetDamage(target);

                    var killWithIgniteAndW = !killWithW && Whit && hasIgnite &&
                                             config.Item("useIgnite", true).GetValue<bool>() &&
                                             R.GetDamage(target) + W.GetDamage(target) + ignitedmg > targetHP &&
                                             targetHP > R.GetDamage(target) + W.GetDamage(target);

                    if (killWithW || (targetHP < R.GetDamage(target) && !justQ && CheckW(target)))
                    {
                        R.CastOnUnit(target);
                    }

                    if ((killWithIgnite || killWithIgniteAndW) && CheckW(target) &&  player.LSDistance(target) < 600)
                    {
                        R.CastOnUnit(target);
                        IgniteTarget = target;
                        Utility.DelayAction.Add(
                            200, () =>
                            {
                                IgniteTarget = null;
                                player.Spellbook.CastSpell(player.LSGetSpellSlot("SummonerDot"), target);
                            });
                    }
                }
                else
                {
                    if (target.Health < R.GetDamage(target))
                    {
                        R.CastOnUnit(target);
                    }
                }
            }
        }


        private bool CheckMana()
        {
            float mana = 0;
            if (Q.LSIsReady())
            {
                mana += Q.ManaCost;
            }
            if (W.LSIsReady())
            {
                mana += W.ManaCost;
            }
            if (E.LSIsReady())
            {
                mana += E.ManaCost;
            }
            if (R.LSIsReady())
            {
                mana += R.ManaCost;
            }
            return mana < player.Mana;
        }

        private void CastE(AIHeroClient target, bool edge = true, int minHits = 1)
        {
            if (Program.IsSPrediction)
            {
                E.SPredictionCastRing(target, 80, HitChance.High, edge);
            }
            else
            {
                if (player.LSCountEnemiesInRange(E.Range + 175) <= 1)
                {
                    var targE = E.GetPrediction(target);
                    var pos = targE.CastPosition;
                    if (pos.LSIsValid() && pos.LSDistance(player.Position) < E.Range &&
                        targE.Hitchance >= HitChance.VeryHigh)
                    {
                        E.Cast(edge ? pos.LSExtend(player.Position, 375) : pos);
                    }
                }
                else
                {
                    var targE = getBestEVector3(target, minHits);
                    if (targE != Vector3.Zero)
                    {
                        E.Cast(targE);
                    }
                }
            }
        }

        private bool CheckW(AIHeroClient target)
        {
            if (justW && W.GetDamage(target) > target.Health && wPos.LSDistance(target.Position) < W.Width)
            {
                return false;
            }
            return true;
        }

        private void CastQHero(AIHeroClient target)
        {
            if (Program.IsSPrediction)
            {
                var pred = Q.GetSPrediction(target);
                if (pred.CollisionResult.Units.Count < 2)
                {
                    Q.Cast(pred.CastPosition);
                }
            }
            else
            {
                var targQ = Q.GetPrediction(target, true);
                var collision = Q.GetCollision(
                    player.Position.LSTo2D(), new List<Vector2>() { targQ.CastPosition.LSTo2D() });
                if (Q.Range - 100 > targQ.CastPosition.LSDistance(player.Position) && collision.Count < 2)
                {
                    Q.CastIfHitchanceEquals(target, HitChance.High);
                }
            }
        }

        private void LastHitQ(bool auto = false)
        {
            if (!Q.LSIsReady())
            {
                return;
            }
            if (auto && player.ManaPercent < config.Item("autoQmana", true).GetValue<Slider>().Value)
            {
                return;
            }
            if (config.Item("useqLC", true).GetValue<bool>() || config.Item("useqLH", true).GetValue<bool>() || auto)
            {
                var minions =
                    MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly)
                        .Where(
                            m =>
                                m.LSIsValidTarget() && m.Health > 5 && m.LSDistance(player) < Q.Range &&
                                m.Health <
                                Q.GetDamage(m) * config.Item("qLHDamage", true).GetValue<Slider>().Value / 100);
                var objAiBases = from minion in minions
                    let pred =
                        Q.GetCollision(
                            player.Position.LSTo2D(),
                            new List<Vector2>() { player.Position.LSExtend(minion.Position, Q.Range).LSTo2D() }, 70f)
                    orderby pred.Count descending
                    select minion;
                if (objAiBases.Any())
                {
                    Obj_AI_Base target = null;
                    foreach (var minion in
                        objAiBases.Where(
                            minion =>
                                HealthPrediction.GetHealthPrediction(
                                    minion, (int) (minion.LSDistance(player) / Q.Speed * 1000 + 500f)) > 0))
                    {
                        var collision =
                            Q.GetCollision(
                                player.Position.LSTo2D(),
                                new List<Vector2>() { player.Position.LSExtend(minion.Position, Q.Range).LSTo2D() }, 70f)
                                .OrderBy(c => c.LSDistance(player))
                                .ToList();
                        if (collision.Count <= 2 || collision[0].NetworkId == minion.NetworkId ||
                            collision[1].NetworkId == minion.NetworkId)
                        {
                            if (collision.Count == 1)
                            {
                                Q.Cast(minion);
                            }
                            else
                            {
                                var other = collision.FirstOrDefault(c => c.NetworkId != minion.NetworkId);
                                if (other != null &&
                                    (player.LSGetAutoAttackDamage(other) * 2 > other.Health - Q.GetDamage(other)) &&
                                    Q.GetDamage(other) < other.Health)
                                {
                                    qMiniForWait = other;
                                    qMiniTarget = minion;
                                    if (Orbwalking.CanAttack() &&
                                        other.LSDistance(player) < Orbwalking.GetRealAutoAttackRange(other))
                                    {
                                        Player.IssueOrder(GameObjectOrder.AutoAttack, other);
                                    }
                                }
                                else
                                {
                                    Q.Cast(minion);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void Game_OnDraw(EventArgs args)
        {
            DrawHelper.DrawCircle(config.Item("drawqq", true).GetValue<Circle>(), Q.Range);
            DrawHelper.DrawCircle(config.Item("drawww", true).GetValue<Circle>(), W.Range);
            DrawHelper.DrawCircle(config.Item("drawee", true).GetValue<Circle>(), 700f);
            DrawHelper.DrawCircle(config.Item("drawrr", true).GetValue<Circle>(), R.Range);
            HpBarDamageIndicator.Enabled = config.Item("drawcombo", true).GetValue<bool>();
            if (wPos.LSIsValid() && config.Item("drawW", true).GetValue<bool>())
            {
                Render.Circle.DrawCircle(wPos, W.Width, Color.Blue, 8);
            }
        }

        private IEnumerable<Vector3> GetEpoints(AIHeroClient target)
        {
            var targetPos = E.GetPrediction(target);
            return
                CombatHelper.PointsAroundTheTargetOuterRing(targetPos.CastPosition, 345, 16)
                    .Where(p =>  player.LSDistance(p) < 700);
        }

        private static float ComboDamage(AIHeroClient hero)
        {
            double damage = 0;
            if (Q.LSIsReady())
            {
                damage += Damage.LSGetSpellDamage(player, hero, SpellSlot.Q);
            }
            if (W.LSIsReady())
            {
                damage += Damage.LSGetSpellDamage(player, hero, SpellSlot.W);
            }
            if (R.LSIsReady())
            {
                damage += Damage.LSGetSpellDamage(player, hero, SpellSlot.R);
            }
            //damage += ItemHandler.GetItemsDamage(hero);
            var ignitedmg = player.GetSummonerSpellDamage(hero, Damage.SummonerSpell.Ignite);
            if (player.Spellbook.CanUseSpell(player.LSGetSpellSlot("summonerdot")) == SpellState.Ready &&
                hero.Health < damage + ignitedmg)
            {
                damage += ignitedmg;
            }
            return (float) damage;
        }

        private Vector3 getBestEVector3(AIHeroClient target, int minHits = 1)
        {
            var points = GetEpoints(target);
            var otherHeroes =
                HeroManager.Enemies.Where(
                    e => e.LSIsValidTarget() && e.NetworkId != target.NetworkId &&  player.LSDistance(e) < 1000)
                    .Select(e => E.GetPrediction(e));

            var targetList = new List<EData>();

            if (otherHeroes.Any())
            {
                foreach (var point in points)
                {
                    targetList.Add(
                        new EData(
                            point,
                            otherHeroes.Count(
                                otherHero =>
                                    otherHero.CastPosition.LSDistance(point) > 345 &&
                                    otherHero.CastPosition.LSDistance(point) < 375), point.LSCountEnemiesInRange(345)));
                }
            }

            var result = targetList.Where(t => t.hits >= minHits).OrderByDescending(t => t.hits).FirstOrDefault();
            if (result != null)
            {
                return result.point;
            }
            if (minHits > 1)
            {
                var result2 =
                    targetList.Where(t => t.hits >= 1 && t.enemiesAround >= minHits)
                        .OrderByDescending(t => t.hits)
                        .ThenByDescending(t => t.enemiesAround)
                        .FirstOrDefault();
                if (result2 != null)
                {
                    return result2.point;
                }
            }
            return Vector3.Zero;
        }

        private void InitMenu()
        {
            config = new Menu("Veigar ", "Veigar", true);
            // Target Selector
            Menu menuTS = new Menu("Selector", "tselect");
            TargetSelector.AddToMenu(menuTS);
            config.AddSubMenu(menuTS);
            // Orbwalker
            Menu menuOrb = new Menu("Orbwalker", "orbwalker");
            orbwalker = new Orbwalking.Orbwalker(menuOrb);
            config.AddSubMenu(menuOrb);
            // Draw settings
            Menu menuD = new Menu("Drawings ", "dsettings");
            menuD.AddItem(new MenuItem("drawqq", "Draw Q range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 100, 146, 166)));
            menuD.AddItem(new MenuItem("drawww", "Draw W range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 100, 146, 166)));
            menuD.AddItem(new MenuItem("drawee", "Draw E range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 100, 146, 166)));
            menuD.AddItem(new MenuItem("drawrr", "Draw R range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 100, 146, 166)));
            menuD.AddItem(new MenuItem("drawW", "Draw W Area", true)).SetValue(true);
            menuD.AddItem(new MenuItem("drawcombo", "Draw combo damage", true)).SetValue(true);
            config.AddSubMenu(menuD);
            // Combo Settings
            Menu menuC = new Menu("Combo ", "csettings");
            menuC.AddItem(new MenuItem("useq", "Use Q", true)).SetValue(true);
            menuC.AddItem(new MenuItem("usew", "Use W", true)).SetValue(false);
            menuC.AddItem(new MenuItem("usee", "Use E", true)).SetValue(true);
            menuC.AddItem(new MenuItem("useekill", "   Only for kill", true)).SetValue(true);
            menuC.AddItem(new MenuItem("useemin", "   Or AOE min", true)).SetValue(new Slider(1, 1, 5));
            menuC.AddItem(new MenuItem("useEkey", "   Manual cast", true))
                .SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press))
                .SetFontStyle(System.Drawing.FontStyle.Bold, SharpDX.Color.Orange);
            menuC.AddItem(new MenuItem("eType", "   E type", true))
                .SetValue(new StringList(new[] { "Cast on Edge", "Trap the enemy" }, 0));
            menuC.AddItem(new MenuItem("predType", "   Prediction", true))
                .SetValue(new StringList(new[] { "10/10 bots", "They said this is better" }, 0));
            menuC.AddItem(new MenuItem("user", "Use R", true)).SetValue(true);
            menuC.AddItem(new MenuItem("userPred", "   Calc ignite+W to damage", true)).SetValue(true);
            menuC.AddItem(new MenuItem("startWithE", "Start combo with E", true)).SetValue(false);
            menuC.AddItem(new MenuItem("checkmana", "   Check mana", true)).SetValue(true);
            menuC.AddItem(new MenuItem("useIgnite", "Use Ignite", true)).SetValue(true);
            var sulti = new Menu("TeamFight Ult block", "dontult");
            foreach (var hero in ObjectManager.Get<AIHeroClient>().Where(hero => hero.IsEnemy))
            {
                sulti.AddItem(new MenuItem("ult" + hero.BaseSkinName, hero.BaseSkinName, true)).SetValue(false);
            }
            menuC.AddSubMenu(sulti);
            menuC = ItemHandler.addItemOptons(menuC);
            config.AddSubMenu(menuC);
            // Harass Settings
            Menu menuH = new Menu("Harass ", "Hsettings");
            menuH.AddItem(new MenuItem("useqH", "Use Q", true)).SetValue(true);
            menuH.AddItem(new MenuItem("usewH", "Use W", true)).SetValue(true);
            menuH.AddItem(new MenuItem("minmanaH", "Keep X% mana", true)).SetValue(new Slider(1, 1, 100));
            config.AddSubMenu(menuH);
            // LaneClear Settings
            Menu menuLC = new Menu("LaneClear ", "Lcsettings");
            menuLC.AddItem(new MenuItem("useqLC", "Use Q", true)).SetValue(true);
            menuLC.AddItem(new MenuItem("usewLC", "Use W", true)).SetValue(true);
            menuLC.AddItem(new MenuItem("wMinHit", "   W min hit", true)).SetValue(new Slider(3, 1, 6));
            menuLC.AddItem(new MenuItem("minmana", "Keep X% mana", true)).SetValue(new Slider(1, 1, 100));
            config.AddSubMenu(menuLC);
            // Lasthit Settings
            Menu menuLH = new Menu("Lasthit ", "Lasthcsettings");
            menuLH.AddItem(new MenuItem("useqLH", "Use Q", true)).SetValue(true);
            menuLH.AddItem(new MenuItem("qLHDamage", "   Q lasthit damage percent", true))
                .SetValue(new Slider(100, 1, 100));
            menuLH.AddItem(new MenuItem("useqLHinHarass", "LastHit in harass", true)).SetValue(true);
            menuLH.AddItem(new MenuItem("minmanaLH", "Keep X% mana", true)).SetValue(new Slider(1, 1, 100));
            config.AddSubMenu(menuLH);
            Menu menuM = new Menu("Misc ", "Msettings");
            menuM.AddItem(new MenuItem("autoQ", "Auto Q lasthit", true)).SetValue(true);
            menuM.AddItem(new MenuItem("autoQmana", "   Keep X% mana", true)).SetValue(new Slider(1, 1, 100));
            menuM.AddItem(new MenuItem("autoW", "Auto W on stun", true)).SetValue(true);
            menuM.AddItem(new MenuItem("autoE", "Auto E on stun", true)).SetValue(true);
            menuM.AddItem(new MenuItem("Interrupt", "Cast E to interrupt spells", true)).SetValue(true);
            menuM.AddItem(new MenuItem("GapCloser", "Cast E on gapclosers", true)).SetValue(true);
            menuM.AddItem(new MenuItem("OnDash", "Cast E on dash", true)).SetValue(true);
            Menu menuKS = new Menu("KS ", "Kill steal");
            menuKS.AddItem(new MenuItem("ksQ", "Use Q", true)).SetValue(false);
            menuKS.AddItem(new MenuItem("ksR", "Use R", true)).SetValue(false);
            menuM.AddSubMenu(menuKS);
            menuM = DrawHelper.AddMisc(menuM);
            config.AddSubMenu(menuM);
            
            config.AddItem(new MenuItem("UnderratedAIO", "by Soresu v" + Program.version.ToString().Replace(",", ".")));
            config.AddToMainMenu();
            switch (config.Item("predType", true).GetValue<StringList>().SelectedIndex)
            {
                case 0:
                    E.SetSkillshot(1.2f, 25f, float.MaxValue, false, SkillshotType.SkillshotCircle);
                    break;
                case 1:
                    E.SetSkillshot(0.85f, 50f, float.MaxValue, false, SkillshotType.SkillshotCircle);
                    break;
            }
            config.Item("predType", true).ValueChanged += OnValueChanged;
        }

        private void OnValueChanged(object sender, OnValueChangeEventArgs onValueChangeEventArgs)
        {
            switch (onValueChangeEventArgs.GetNewValue<StringList>().SelectedIndex)
            {
                case 0:
                    E.SetSkillshot(1.2f, 25f, float.MaxValue, false, SkillshotType.SkillshotCircle);
                    break;
                case 1:
                    E.SetSkillshot(0.85f, 50f, float.MaxValue, false, SkillshotType.SkillshotCircle);
                    break;
            }
        }
    }

    internal class EData
    {
        public Vector3 point;
        public int hits;
        public int enemiesAround;

        public EData(Vector3 _point, int _hits, int _enemiesAround)
        {
            hits = _hits;
            point = _point;
            enemiesAround = _enemiesAround;
        }
    }
}