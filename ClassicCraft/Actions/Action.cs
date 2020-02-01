﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassicCraft
{
    public enum ResultType
    {
        Miss,
        Dodge,
        Parry,
        Glance,
        Block,
        Crit,
        Hit,
        Resist
    }

    public class ActionResult
    {
        public ResultType Type { get; set; }
        public int Damage { get; set; }
        public ActionResult(ResultType type, int damage)
        {
            Type = type;
            Damage = damage;
        }
    }

    public abstract class Action : PlayerObject
    {
        public double BaseCD { get; set; }

        public double LockedUntil { get; set; }

        public School School { get; set; }

        public Action(Player p, double baseCD, School school = School.Physical)
            : base(p)
        {
            BaseCD = baseCD;
            LockedUntil = 0;
            School = school;
        }

        public abstract void Cast();

        public abstract void DoAction();

        public abstract bool CanUse();

        public void CommonAction()
        {
            CDAction();
            Player.StartGCD();
        }

        public void CDAction()
        {
            LockedUntil = Player.Sim.CurrentTime + BaseCD;
        }

        public bool Available()
        {
            return LockedUntil <= Player.Sim.CurrentTime;
        }

        public double RemainingCD()
        {
            return LockedUntil - Player.Sim.CurrentTime;
        }

        public string ResourceName()
        {
            switch (Player.Class)
            {
                case Player.Classes.Druid: return "energy";
                case Player.Classes.Rogue: return "energy";
                case Player.Classes.Warrior: return "rage";
                default: return "mana";
            }
        }

        public virtual void RegisterDamage(ActionResult res)
        {
            Player.Sim.RegisterAction(new RegisteredAction(this, res, Player.Sim.CurrentTime));

            if(Program.logFight)
            {
                string log = string.Format("{0:N2} : {1} {2} for {3} damage", Player.Sim.CurrentTime, ToString(), res.Type, res.Damage);
                if (!ResourceName().Equals("mana"))
                {
                    log += string.Format(" ({0} {1}/{2})", ResourceName(), Player.Resource, Player.MaxResource);
                }
                if(Player.Form == Player.Forms.Cat || Player.Class == Player.Classes.Rogue)
                {
                    log += " [combo " + Player.Combo + "]";
                }
                if (Player.Mana > 0)
                {
                    log += " - Mana " + Player.Mana + "/" + Player.MaxMana;
                }
                Program.Log(log);
            }
        }

        public virtual void LogAction()
        {
            if(Program.logFight)
            {
                string log = string.Format("{0:N2} : {1} cast ({2} {3}/{4})", Player.Sim.CurrentTime, ToString(), ResourceName(), Player.Resource, Player.MaxResource);
                if (Player.Form == Player.Forms.Cat || Player.Class == Player.Classes.Rogue)
                {
                    log += "[combo " + Player.Combo + "]";
                }
                if(Player.Mana > 0)
                {
                    log += " - Mana " + Player.Mana + "/" + Player.MaxMana;
                }
                Program.Log(log);
            }
        }

        public override string ToString()
        {
            return "Undefined Action";
        }
    }
}
