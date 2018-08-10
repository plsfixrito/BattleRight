﻿using System;
using System.Linq;
using System.Text;
using BattleRight.Core;
using BattleRight.Core.Enumeration;
using BattleRight.Core.GameObjects;
using BattleRight.Core.GameObjects.Models;
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
		public enum TargetingOrder
		{
			EnemyAllyOrb,
			EnemyOrbAlly,
			OrbEnemyAlly,
			AllyEnemyOrb,
			AllyOrbEnemy,
			OrbAllyEnemy,
		}

		internal static bool IsPoloma;
		internal static bool EditingAim, StartedCast;

		internal static SkillBase lmbSkill, rmbSkill, spaceSkill, qSkill, eSkill, rSkill, ex1Skill, ex2Skill, fSkill;
		internal static Menu PolomaMenu, ComboMenu, PlayersMenu, DrawMenu;
		internal static MenuCheckBox UseLmb, LmbEnemy, LmbAlly, LmbOrb, LmbHealStop, UseQ, UseExQ, DrawLmb, DrawQ, DrawAim;
		internal static MenuKeybind ComboKey, AllyKey, EnemyKey;
		internal static MenuComboBox LmbTo;
		internal static MenuSlider ExQForce;
		internal static MenuIntSlider QCount, ExQCount;
		internal static PredictionOutput LastOutput;
		internal static Color HotPink = new Color(1f, 0.4117647058823529f, 0.7058823529411765f, 1f);

		public void OnInit()
		{
			if (!CreateMenu())
				Console.WriteLine("Kappa Poloma: Menu creation failed");
			if (!CreateSkills())
				Console.WriteLine("Kappa Poloma: Skills creation failed");

			Game.OnMatchStart += delegate(EventArgs args)
			                     {
				                     IsPoloma = LocalPlayer.Instance != null &&
				                                LocalPlayer.Instance.ChampionEnum == Champion.Poloma;

				                     if (IsPoloma)
				                     {
					                     Game.OnUpdate += GameOnOnUpdate;
					                     Game.OnDraw += GameOnOnDraw;
										 
					                     PlayersMenu.AddLabel(" - Local Team");
					                     foreach (var player in EntitiesManager.LocalTeam)
					                     {
						                     if (!player.IsLocalPlayer)
											 {
												 PlayersMenu.Add(new MenuCheckBox(player.Name + "." + player.ObjectName, "Heal " + player.Name + " (" + player.ObjectName + ")"));
											 }
										 }

					                     PlayersMenu.AddSeparator(5);
					                     PlayersMenu.AddLabel(" - Enemy Team");
										 foreach (var player in EntitiesManager.EnemyTeam)
										 {
											 PlayersMenu.Add(new MenuCheckBox(player.Name + "." + player.ObjectName, "Target " + player.Name + " (" + player.ObjectName + ")"));
										 }
									 } else
				                     {
					                     Game.OnUpdate -= GameOnOnUpdate;
					                     Game.OnDraw -= GameOnOnDraw;

					                     var children = PlayersMenu.Children.ToList();
										 foreach (var child in children)
						                     PlayersMenu.RemoveItem(child.Name);
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
			if (DrawLmb)
			{
				Drawing.DrawCircle(LocalPlayer.Instance.MapObject.Position, lmbSkill.Range, Color.cyan);
			}
			if (DrawQ)
			{
				Drawing.DrawCircle(LocalPlayer.Instance.MapObject.Position, qSkill.Range, Color.magenta);
			}
			

			if (LastOutput == null)
				return;

			if (DrawAim)
				Drawing.DrawCircle(LastOutput.PredictedPosition, 0.75f, Color.red);
		}

		private void GameOnOnUpdate(EventArgs args)
		{
			if (!LocalPlayer.Instance.AbilitySystem.CanCastAbilities || LocalPlayer.Instance.HasCCOfType(CCType.SpellBlock) ||
			    LocalPlayer.Instance.HasCc("PANIC"))
			{
				AbortMission();
				return;
			}

			if (AllyKey)
			{
				if (!TargetAlly())
					AbortMission();

				return;
			}

			if (EnemyKey)
			{
				if (!TargetEnemy())
					AbortMission();

				return;
			}

			if (!ComboKey)
			{
				AbortMission();
				return;
			}

			if (TryQ())
				return;

			if (TryLmb())
				return;

			AbortMission();
		}

		internal static bool TryQ()
		{
			var useq = UseQ && qSkill.IsReady;
			var exqready = ex2Skill.IsReady;
			var useexq = UseExQ && exqready;

			if (!useq && !useexq)
				return false;

			var count = EntitiesManager.EnemyTeam.Count(e => e.Distance(LocalPlayer.Instance) <= qSkill.Range * 0.9f &&
			                                                 !e.Living.IsDead && !e.PhysicsCollision.IsImmaterial &&
			                                                 !e.SpellCollision.IsUnHitable && !e.SpellCollision.IsUnTargetable);

			if (useexq && count >= ExQCount)
			{
				ex2Skill.Cast();
				StartedCast = true;
				LastOutput = null;

				return true;
			}

			var forceexq = exqready && LocalPlayer.Instance.Living.HealthPercent <= ExQForce;

			if ((useq || forceexq) && count >= QCount)
			{
				if (forceexq)
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
			if (!UseLmb || !lmbSkill.IsReady)
				return false;

			switch((TargetingOrder) LmbTo.CurrentValue)
			{
				case TargetingOrder.AllyEnemyOrb:
					return TargetAlly() || TargetEnemy() || TargetOrb();

				case TargetingOrder.AllyOrbEnemy:
					return TargetAlly() || TargetOrb() || TargetEnemy();

				case TargetingOrder.OrbAllyEnemy:
					return TargetOrb() || TargetAlly() || TargetEnemy();

				case TargetingOrder.EnemyAllyOrb:
					return TargetEnemy() || TargetAlly() || TargetOrb();

				case TargetingOrder.EnemyOrbAlly:
					return TargetEnemy() || TargetOrb() || TargetAlly();

				case TargetingOrder.OrbEnemyAlly:
					return TargetOrb() || TargetEnemy() || TargetAlly();
			}

			AbortMission();
			return true;
		}

		internal static bool TargetOrb()
		{
			if (!LmbOrb)
				return false;

			var orb = EntitiesManager.CenterOrb;

			if (orb == null || !orb.IsValid || orb.Get<LivingObject>().IsDead ||
			    !(orb.Get<MapGameObject>().Position.Distance(LocalPlayer.Instance) < lmbSkill.Range))
				return false;

			LocalPlayer.Aim(orb.Get<MapGameObject>().Position);
			lmbSkill.Cast();
			EditingAim = true;
			StartedCast = true;

			return true;

		}

		internal static bool TargetEnemy()
		{
			if (!LmbEnemy)
				return false;

			var target =
				TargetSelector
				.GetTarget(EntitiesManager.EnemyTeam.Where(e => PlayersMenu.Get<MenuCheckBox>(e.Name + "." + e.ObjectName) && 
				                                                ValidateTarget(e) && !(LastOutput = lmbSkill.GetPrediction(LocalPlayer.Instance, e)).CollisionResult.IsColliding),
				           TargetingMode.LowestHealth, lmbSkill.Range);

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

		internal static bool TargetAlly()
		{
			if (!LmbAlly)
				return false;

			var needHeal = CurrentHealthPercent(LocalPlayer.Instance) < 0.98;
			var target =
				TargetSelector
				.GetTarget(EntitiesManager.LocalTeam.Where(e => !e.IsLocalPlayer &&
				                                                PlayersMenu.Get<MenuCheckBox>(e.Name + "." + e.ObjectName) &&
																(needHeal || !LmbHealStop || CurrentHealthPercent(e) < 0.98f) &&
				                                                ValidateTarget(e)), TargetingMode.LowestHealth, lmbSkill.Range);

			if (target == null)
				return false;

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
				ComboKey = ComboMenu.Add(new MenuKeybind("comboKey", "Use Auto Combo", KeyCode.LeftShift));

				ComboMenu.AddLabel(" - LMB Settings");
				UseLmb = ComboMenu.Add(new MenuCheckBox("use.lmb", "Use LMB"));
				LmbEnemy = ComboMenu.Add(new MenuCheckBox("lmb.enemy", "Use On Enemies"));
				LmbAlly = ComboMenu.Add(new MenuCheckBox("lmb.ally", "Use On Allies if no Enemy is found"));
				LmbOrb = ComboMenu.Add(new MenuCheckBox("lmb.orb", "Use On Orb if no Ally/Enemy is found"));
				LmbHealStop = ComboMenu.Add(new MenuCheckBox("lmb.healstop", "Don't Try Heal Full Health Allies", false));
				AllyKey = ComboMenu.Add(new MenuKeybind("ally.key", "LMB On Allies", KeyCode.X));
				EnemyKey = ComboMenu.Add(new MenuKeybind("enemy.key", "LMB On Enemies", KeyCode.V));
				LmbTo = ComboMenu.Add(new MenuComboBox("lmb.to", "Combo Target Order", 0,
				                               Enum.GetNames(typeof(TargetingOrder)).Select(s => InsertBeforeUpperCase(s, " > "))
				                                   .ToArray()));
				ComboMenu.AddSeparator(10);

				ComboMenu.AddLabel(" - Q Settings");
				UseQ = ComboMenu.Add(new MenuCheckBox("use.q", "Use Q"));
				QCount = ComboMenu.Add(new MenuIntSlider("use.q.count", "Use Q Enemies", 1, 3, 1));
				UseExQ = ComboMenu.Add(new MenuCheckBox("use.qex", "Use EX Q"));
				ExQCount = ComboMenu.Add(new MenuIntSlider("use.qex.count", "Use EX Q Enemies", 2, 3, 1));
				ExQForce = ComboMenu.Add(new MenuSlider("use.qex.force", "Force EX Q HP%", 50, 100));

				PolomaMenu.Add(ComboMenu);

				PlayersMenu = new Menu("Players", "Targeting");


				PolomaMenu.Add(PlayersMenu);

				DrawMenu = new Menu("Drawing", "Drawings");
				DrawLmb = DrawMenu.Add(new MenuCheckBox("draw.lmb", "Draw LMB Range"));
				DrawQ = DrawMenu.Add(new MenuCheckBox("draw.q", "Draw Q/EXQ Range"));
				DrawAim = DrawMenu.Add(new MenuCheckBox("draw.aim", "Draw Current Aiming Position"));

				PolomaMenu.Add(DrawMenu);
				return true;
			} catch (Exception e)
			{
				Console.WriteLine(e);

				return false;
			}
		}

		internal static bool CreateSkills()
		{
			try
			{
				lmbSkill = new SkillBase(AbilitySlot.Ability1, SkillType.Line, 8.5f, 15.5f, .2f);
				rmbSkill = new SkillBase(AbilitySlot.Ability2, SkillType.Circle, int.MaxValue, 4, .2f);
				spaceSkill = new SkillBase(AbilitySlot.Ability3, SkillType.Line, 9f, 4, .2f);
				qSkill = new SkillBase(AbilitySlot.Ability4, SkillType.Circle, 2.5f, int.MaxValue, .2f, 100);
				eSkill = new SkillBase(AbilitySlot.Ability5, SkillType.Line, 9.5f, 4, .2f);
				fSkill = new SkillBase(AbilitySlot.Ability6, SkillType.Circle, 7f, int.MaxValue, .2f, 25f);
				ex2Skill = new SkillBase(AbilitySlot.EXAbility2, SkillType.Circle, 2.5f, int.MaxValue, .2f, 100)
				           { GetAbilityHudByName = "SoulDrainAbility" };

				return true;
			} catch (Exception e)
			{
				Console.WriteLine(e);

				return false;
			}
		}

		internal static void AbortMission()
		{
			if (StartedCast)
			{
				if (!LocalPlayer.Instance.HasCC || !LocalPlayer.Instance.CCName.StartsWith("OTHER"))
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

		internal static bool ValidateTarget(Character character)
		{
			if (character.Living.IsDead)
				return false;
			var spellCol = character.SpellCollision;

			return !spellCol.IsUnHitable && !spellCol.IsUnTargetable && !character.PhysicsCollision.IsImmaterial &&
			       !character.HasCCOfType(CCType.Consume) && !character.HasCCOfType(CCType.Parry) &&
			       !character.HasCCOfType(CCType.Counter);
		}

		internal static float CurrentHealthPercent(Character character)
		{
			var living = character.Living;

			return living.Health / living.MaxRecoveryHealth;
		}

		public static string InsertBeforeUpperCase(string str, string toInsert)
		{
			var sb = new StringBuilder();

			char previousChar = char.MinValue;

			foreach (char c in str)
			{
				if (char.IsUpper(c))
				{
					if (sb.Length != 0 && previousChar != ' ')
					{
						foreach (var t in toInsert)
						{
							sb.Append(t);
						}
					}
				}

				sb.Append(c);

				previousChar = c;
			}

			return sb.ToString();
		}

		public void OnUnload()
		{
		}
	}
}
