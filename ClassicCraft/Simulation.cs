﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassicCraft
{
    public class Simulation
    {
        public static double RATE = 20;

        public Player Player { get; set; }
        public Boss Boss { get; set; }
        public double FightLength { get; set; }

        private List<RegisteredAction> Actions { get; set; }
        private List<RegisteredEffect> Effects { get; set; }
        private double Damage { get; set; }

        public double CurrentTime { get; set; }

        public bool Ended { get; set; }

        public Random random = new Random();

        public Simulation(Player player, Boss boss, double fightLength)
        {
            Player = player;
            Boss = boss;
            player.Sim = this;
            Boss.Sim = this;
            FightLength = fightLength;
            Actions = new List<RegisteredAction>();
            Effects = new List<RegisteredEffect>();
            Damage = 0;
            CurrentTime = 0;
            Ended = false;
        }

        public void StartSim()
        {
            List<AutoAttack> autos = new List<AutoAttack>();

            autos.Add(new AutoAttack(Player, Player.MH, true));
            if (Player.OH != null)
            {
                autos.Add(new AutoAttack(Player, Player.OH, false));
            }

            CurrentTime = 0;

            Whirlwind ww = new Whirlwind(Player);
            Bloodthirst bt = new Bloodthirst(Player);
            HeroicStrike hs = new HeroicStrike(Player);
            hs.RessourceCost -= Player.GetTalentPoints("IHS");
            Execute exec = new Execute(Player);
            Bloodrage br = new Bloodrage(Player);
            BattleShout bs = new BattleShout(Player);

            Dictionary<Spell, int> cds = new Dictionary<Spell, int>()
            {
                { new DeathWish(Player), DeathWishBuff.LENGTH },
                { new JujuFlurry(Player), JujuFlurryBuff.LENGTH },
                { new MightyRage(Player), MightyRageBuff.LENGTH },
                { new Recklessness(Player), RecklessnessBuff.LENGTH },
                { new BloodFury(Player), BloodFuryBuff.LENGTH },
            };

            Boss.LifePct = 1;

            // Pre-cast Battle Shout (starts GCD as Charge would)
            bs.Cast();

            // Charge
            Player.Ressource += 15;

            int rota = 1;

            while (CurrentTime < FightLength)
            {
                Boss.LifePct = Math.Max(0, 1 - (CurrentTime / FightLength) * (16.0 / 17.0));
                
                foreach (Effect e in Player.Effects)
                {
                    e.CheckEffect();
                }
                foreach (Effect e in Boss.Effects)
                {
                    e.CheckEffect();
                }

                Player.Effects.RemoveAll(e => e.Ended);
                Boss.Effects.RemoveAll(e => e.Ended);

                if (br.CanUse())
                {
                    br.Cast();
                }

                if (bs.CanUse() && (!Player.Effects.Any(e => e is BattleShoutBuff) || ((BattleShoutBuff)Player.Effects.Where(e => e is BattleShoutBuff).First()).RemainingTime() < Player.GCD))
                {
                    bs.Cast();
                }


                foreach(Spell cd in cds.Keys)
                {
                    if(cd.CanUse() &&
                        (FightLength - CurrentTime <= cds[cd]
                        || FightLength - CurrentTime >= cd.BaseCD + cds[cd]))
                    {
                        cd.Cast();
                    }
                }

                if (rota == 0)
                {

                }
                else if (rota == 1)
                {
                    if (Boss.LifePct > 0.2)
                    {
                        if (ww.CanUse())
                        {
                            ww.Cast();
                        }
                        else if (bt.CanUse() && Player.Ressource >= ww.RessourceCost + bt.RessourceCost)
                        {
                            bt.Cast();
                        }

                        if (Player.Ressource >= 75)
                        {
                            hs.Cast();
                        }
                    }
                    else
                    {
                        if (exec.CanUse())
                        {
                            exec.Cast();
                        }
                    }
                }
                else if (rota == 2)
                {
                    if (bt.CanUse())
                    {
                        bt.Cast();
                    }
                    else if (ww.CanUse() && Player.Ressource >= ww.RessourceCost + bt.RessourceCost)
                    {
                        ww.Cast();
                    }

                    if (Player.Ressource >= 75)
                    {
                        hs.Cast();
                    }
                }

                foreach (AutoAttack a in autos)
                {
                    if (a.Available())
                    {
                        if (a.MH && Player.applyAtNextAA != null)
                        {
                            Player.applyAtNextAA.DoAction();
                            a.NextAA();
                        }
                        else
                        {
                            a.Cast();
                        }
                    }
                }

                Player.Effects.RemoveAll(e => e.Ended);
                Boss.Effects.RemoveAll(e => e.Ended);

                CurrentTime += 1 / RATE;
            }

            Program.damages.Add(Damage);
            Program.totalActions.Add(Actions);
            Program.totalEffects.Add(Effects);

            Ended = true;
        }

        public void RegisterAction(RegisteredAction action)
        {
            Actions.Add(action);
            Damage += action.Result.Damage;
        }

        public void RegisterEffect(RegisteredEffect effect)
        {
            Effects.Add(effect);
            Damage += effect.Damage;
        }

        public static double Normalization(Weapon w)
        {
            if (w.Type == Weapon.WeaponType.Dagger)
            {
                return 1.7;
            }
            else if (w.TwoHanded)
            {
                return 3.3;
            }
            else
            {
                return 2.4;
            }
        }

        public double DamageMod(ResultType type, int level = 60, int enemyLevel = 63)
        {
            switch (type)
            {
                case ResultType.Crit: return 2;
                case ResultType.Hit: return 1;
                case ResultType.Glancing: return GlancingDamage(level, enemyLevel);
                default: return 0;
            }
        }

        public double GlancingDamage(int level = 60, int enemyLevel = 63)
        {
            double low = Math.Max(0.01, Math.Min(0.91, 1.3 - 0.05 * (enemyLevel - level)));
            double high = Math.Max(0.2, Math.Min(0.99, 1.2 - 0.03 * (enemyLevel - level)));
            return random.NextDouble() * (high - low) + low;
        }

        public static double RageGained(int damage, double weaponSpeed, ResultType type, bool mh = true)
        {
            return (15 * damage) / (4 * RageConversionValue()) + (RageWhiteHitFactor(mh, type == ResultType.Crit) * weaponSpeed) / 2;
        }

        public static double RageGained2(int damage)
        {
            return (15 * damage) / RageConversionValue();
        }

        public static double RageConversionValue(int level = 60)
        {
            return 0.0091107836 * level * level + 3.225598133 * level + 4.2652911;
        }

        public static double RageWhiteHitFactor(bool mh, bool crit)
        {
            if (mh)
            {
                if (crit)
                {
                    return 7.0;
                }
                else
                {
                    return 3.5;
                }
            }
            else
            {
                if (crit)
                {
                    return 3.5;
                }
                else
                {
                    return 1.75;
                }
            }
        }
    }
}
