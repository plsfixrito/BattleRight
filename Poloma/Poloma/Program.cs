using System;
using System.Linq;
using BattleRight.Core;
using BattleRight.Core.Enumeration;
using BattleRight.Core.GameObjects;
using BattleRight.Sandbox;
using BattleRight.SDK;
using BattleRight.SDK.Enumeration;
using BattleRight.SDK.UI;
using BattleRight.SDK.UI.Models;
using BattleRight.SDK.UI.Values;
using UnityEngine;

namespace Poloma
{
    public class Program : IAddon
    {
        internal static bool IsPoloma;
        internal static bool EditingAim, StartedCast;

        internal static SkillBase lmbSkill, rmbSkill, spaceSkill, qSkill, eSkill, rSkill, ex1Skill, ex2Skill, fSkill;
        internal static Menu PolomaMenu, ComboMenu;
        internal static PredictionOutput LastOutput;

        public void OnInit()
        {
            if (!CreateMenu())
                Console.WriteLine("Kappa Poloma: Menu creation failed");
            if (!CreateSkills())
                Console.WriteLine("Kappa Poloma: Skills creation failed");

            Game.OnMatchStart += delegate (EventArgs args)
			{
				IsPoloma = LocalPlayer.Instance != null &&
				           LocalPlayer.Instance.ChampionEnum == Champion.Poloma;
				
				if (IsPoloma)
			    {
			        Game.OnUpdate += GameOnOnUpdate;
			        Game.OnDraw += GameOnOnDraw;
			    } else
			    {
			        Game.OnUpdate -= GameOnOnUpdate;
			        Game.OnDraw -= GameOnOnDraw;
			    }
			};
            Game.OnMatchEnd += delegate(EventArgs args)
			{
                Game.OnUpdate -= GameOnOnUpdate;
                Game.OnDraw -= GameOnOnDraw;
            };
        }

        private void GameOnOnDraw(EventArgs args)
        {
            if(LastOutput == null)
                return;

            Drawing.DrawCircle(LastOutput.PredictedPosition, 1, Color.red);
        }

        private void GameOnOnUpdate(EventArgs args)
        {
            if (!ComboMenu.Get<MenuKeybind>("comboKey"))
            {
                AbortMission();
                return;
            }
            
            if (!LocalPlayer.Instance.AbilitySystem.CanCastAbilities ||
                LocalPlayer.Instance.HasCCOfType(CCType.SpellBlock))
            {
                AbortMission();
                return;
            }
            
            if(TryQ())
                return;

            if(TryLmb())
                return;

            AbortMission();
        }

        internal static bool TryQ()
        {
            var useq = ComboMenu.Get<MenuCheckBox>("use.q") && qSkill.IsReady;
            var exqready = ex2Skill.IsReady;
            var useexq = ComboMenu.Get<MenuCheckBox>("use.qex") && exqready;

            if (!useq && !useexq)
                return false;
            
            var count =
                EntitiesManager.EnemyTeam.Count(e => e.Distance(LocalPlayer.Instance) <= qSkill.Range * 0.9f &&
                                                     !e.Living.IsDead &&
                                                     !e.PhysicsCollision.IsImmaterial &&
                                                     !e.SpellCollision.IsUnHitable &&
                                                     !e.SpellCollision.IsUnTargetable);

            if (useexq && count >= ComboMenu.Get<MenuIntSlider>("use.qex.count"))
            {
                ex2Skill.Cast();
                StartedCast = true;
                LastOutput = null;
                return true;
            }

            var forceexq = exqready && LocalPlayer.Instance.Living.HealthPercent <= ComboMenu.Get<MenuSlider>("use.qex.force");

            if ((useq || forceexq) && count >= ComboMenu.Get<MenuIntSlider>("use.q.count"))
            {
                if(forceexq)
                    ex2Skill.Cast();
                else
                    qSkill.Cast();
                StartedCast = true;
                LastOutput = null;
                return true;
            }

            return false;
        }

        internal static bool TryLmb()
        {
            if (!ComboMenu.Get<MenuCheckBox>("use.lmb") || !lmbSkill.IsReady)
                return false;

            Character target = null;
            if (ComboMenu.Get<MenuCheckBox>("lmb.enemy"))
            {
                var targets =
                    EntitiesManager.EnemyTeam.Where(e => !e.Living.IsDead &&
                                                         !e.PhysicsCollision.IsImmaterial &&
                                                         !e.SpellCollision.IsUnHitable &&
                                                         !e.SpellCollision.IsUnTargetable &&
                                                         !e.HasCCOfType(CCType.Consume) &&
                                                         !e.HasCCOfType(CCType.Parry) &&
                                                         !e.HasCCOfType(CCType.Counter) &&
                                                         !(LastOutput = lmbSkill.GetPrediction(LocalPlayer.Instance, e)).CollisionResult.IsColliding);
                target = TargetSelector.GetTarget(targets, TargetingMode.LowestHealth, lmbSkill.Range);
            }

            if (target == null && ComboMenu.Get<MenuCheckBox>("lmb.ally"))
                target = TargetSelector.GetAlly(TargetingMode.LowestHealth, lmbSkill.Range);

            if (target == null)
                return false;

            if (LastOutput == null || LastOutput.Input.Target != target)
                LastOutput = lmbSkill.GetPrediction(LocalPlayer.Instance, target);

            if (LastOutput == null || LastOutput.CollisionResult.IsColliding)
                return false;

            LocalPlayer.Aim(LastOutput.PredictedPosition);
            lmbSkill.Cast();
            EditingAim = true;
            StartedCast = true;
            return true;
        }

        internal static bool CreateMenu()
        {
            try
            {
                PolomaMenu = MainMenu.AddMenu("kappa.Poloma", "Kappa Poloma");

                ComboMenu = new Menu("Combo", "Combo", true);
                ComboMenu.Add(new MenuKeybind("comboKey", "Use Combo", KeyCode.LeftShift));
                ComboMenu.AddLabel(" - LMB Settings");
                ComboMenu.Add(new MenuCheckBox("use.lmb", "Use LMB"));
                ComboMenu.Add(new MenuCheckBox("lmb.enemy", "Use On Enemies"));
                ComboMenu.Add(new MenuCheckBox("lmb.ally", "Use On Allies if no Enemy is found"));
                ComboMenu.AddSeparator(10);

                ComboMenu.AddLabel(" - Q Settings");
                ComboMenu.Add(new MenuCheckBox("use.q", "Use Q"));
                ComboMenu.Add(new MenuIntSlider("use.q.count", "Use Q Enemies", 1, 3, 1));
				ComboMenu.Add(new MenuCheckBox("use.qex", "Use EX Q"));
                ComboMenu.Add(new MenuIntSlider("use.qex.count", "Use EX Q Enemies", 2, 3, 1));
                ComboMenu.Add(new MenuSlider("use.qex.force", "Force EX Q HP%", 50, 100));

                PolomaMenu.Add(ComboMenu);

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        internal static bool CreateSkills()
        {
            try
            {
                lmbSkill = new SkillBase(AbilitySlot.Ability1, SkillType.Line, 8.5f, 4, .2f);
                rmbSkill = new SkillBase(AbilitySlot.Ability2, SkillType.Circle, int.MaxValue, 4, .2f);
                spaceSkill = new SkillBase(AbilitySlot.Ability3, SkillType.Line, 9f, 4, .2f);
                qSkill = new SkillBase(AbilitySlot.Ability4, SkillType.Circle, 2.5f, int.MaxValue, .2f, 100);
                eSkill = new SkillBase(AbilitySlot.Ability5, SkillType.Line, 9.5f, 4, .2f);
                fSkill = new SkillBase(AbilitySlot.Ability6, SkillType.Circle, 7f, int.MaxValue, .2f, 25f);
                ex2Skill = new SkillBase(AbilitySlot.EXAbility2, SkillType.Circle, 2.5f, int.MaxValue, .2f, 100) { GetAbilityHudByName = "SoulDrainAbility" };

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        internal static void AbortMission()
        {
            if (StartedCast)
            {
                if(!LocalPlayer.Instance.HasCC || !LocalPlayer.Instance.CCName.StartsWith("OTHER"))
                    LocalPlayer.PressAbility(AbilitySlot.Interrupt, true);
                StartedCast = false;
            }

            if (EditingAim)
            {
                LocalPlayer.EditAimPosition = false;
                EditingAim = false;
            }

            LastOutput = null;
        }

        public void OnUnload()
        {
        }
    }
}
