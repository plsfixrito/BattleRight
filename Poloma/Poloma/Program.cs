﻿using System;
using System.Collections.Generic;
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
			OrbAllyEnemy
		}

		internal static Dictionary<string, bool> DebuffsDic = new Dictionary<string, bool>
		{
			{ "Panic", true }, { "Frozen", true }, { "Stun", true },
			{ "Incapacitate", true }, { "Venom", true },
			{ "Knockback", false }, { "ShackleDebuff", true },
			{ "GrimoireOfChaosSurgeDebuff", true }, { "SpellBlock", false },
			{ "Immobilize", true }, { "Slow", false },
			{ "DeadlyInjectionBuff", true }, { "CripplingGooDebuff", true },
			{ "Petrify", true }, { "Silence", false },
			{ "BrainBugDebuff", true }, { "ScarabDebuff", false },
			{ "SeismicShockDebuff", false },
			{ "ClawOfTheWickedKnockback", true },
			{ "LunarStrikePetrify", true }, { "AstralBuff", true },
			{ "EntanglingRootsBuff", true }, { "LawBringerInAir", false },
			{ "SheepTrickDebuff", false }
		};
		internal static string[] Debuffs = { };
		internal static string[] ReflectCc = { "GUST", "BULWARK", "RADIANT SHIELD", "TIME BENDER", "BARBED HUSK" };

		internal static bool IsPoloma, Started;
		internal static bool EditingAim, StartedCast;
		internal static bool CastingE => LocalPlayer.Instance != null && LocalPlayer.Instance.AbilitySystem.CastingAbilityIndex == 9;

		internal static SkillBase LmbSkill, RmbSkill, SpaceSkill, QSkill, ESkill, RSkill, Ex1Skill, Ex2Skill, FSkill;
		internal static Menu PolomaMenu, ComboMenu, RmbMenu, RmbTarget, PlayersMenu, DrawMenu;

		internal static MenuCheckBox UseLmb, LmbEnemy, LmbAlly, LmbOrb, LmbHealStop, UseRmb, UseQ, UseQCasting, UseExQ,
		                             DrawLmb, DrawQ, DrawAim;

		internal static MenuKeybind ComboKey, AllyKey, EnemyKey;
		internal static MenuComboBox LmbTo;
		internal static MenuSlider ExQForce, FullHealthCheck;
		internal static MenuIntSlider QCount, ExQCount;
		internal static PredictionOutput LastOutput;
		internal static Color HotPink = new Color(1f, 0.4117647058823529f, 0.7058823529411765f, 1f);

		internal static Character LastRmbTarget;
		internal static float LastRmbRefresh;

		public void OnInit()
		{
			Started = false;
			Debuffs = DebuffsDic.Keys.ToArray();
			if (!CreateMenu())
				Console.WriteLine("Kappa Poloma: Menu creation failed");
			if (!CreateSkills())
				Console.WriteLine("Kappa Poloma: Skills creation failed");

			Game.OnMatchStateUpdate += LoadInGame;
			Game.OnMatchStart += (args) =>
			{
				Started = false;
				LoadInGame(args);
			};
			Game.OnMatchEnd += delegate
			{
				Started = false;
				Game.OnUpdate -= GameOnOnUpdate;
				Game.OnDraw -= GameOnOnDraw;
				var children = PlayersMenu.Children;
				foreach (var child in children)
					PlayersMenu.RemoveItem(child.Name);
				foreach (var child in RmbTarget.Children)
					RmbTarget.RemoveItem(child.Name);
			};
		}

		private void LoadInGame(EventArgs args)
		{
			if (Started)
				return;
			if (!(IsPoloma = LocalPlayer.Instance != null &&
							 LocalPlayer.Instance.ChampionEnum == Champion.Poloma))
			{
				Game.OnUpdate -= GameOnOnUpdate;
				Game.OnDraw -= GameOnOnDraw;
				return;
			}

			if (EntitiesManager.LocalTeam != null)
			{
				//PlayersMenu.AddLabel("- Local Team");

				foreach (var player in EntitiesManager.LocalTeam)
				{
					if (PlayersMenu.Children.All(c => c.Name != $"{player.Name}.{player.ObjectName}.Ally"))
						PlayersMenu.Add(new MenuCheckBox($"{player.Name}.{player.ObjectName}.Ally",
													 "Heal " + player.Name + " (" + player.ObjectName + ")"));

					if (RmbTarget.Children.All(c => c.Name != $"{player.Name}.{player.ObjectName}"))
						RmbTarget.Add(new MenuCheckBox($"{player.Name}.{player.ObjectName}",
					                               "Other side " + $"{player.Name} ({player.ObjectName})"));
				}

				PlayersMenu.AddSeparator(5);
			}

			if (EntitiesManager.EnemyTeam != null)
			{
				//PlayersMenu.AddLabel("- Enemy Team");
				foreach (var player in EntitiesManager.EnemyTeam)
				{
					if (PlayersMenu.Children.All(c => c.Name != $"{player.Name}.{player.ObjectName}.Enemy"))
						PlayersMenu.Add(new MenuCheckBox($"{player.Name}.{player.ObjectName}.Enemy",
														 "Target " + player.Name + " (" + player.ObjectName + ")"));
				}
			}

			Game.OnUpdate += GameOnOnUpdate;
			Game.OnDraw += GameOnOnDraw;
			Started = true;
		}

		public void OnUnload()
		{
		}

		private void GameOnOnDraw(EventArgs args)
		{
			if (LocalPlayer.Instance == null ||
				LocalPlayer.Instance.Living.IsDead)
				return;
			
			if (DrawLmb)
				Drawing.DrawCircle(LocalPlayer.Instance.MapObject.Position, LmbSkill.Range, Color.cyan);

			if (DrawQ)
				Drawing.DrawCircle(LocalPlayer.Instance.MapObject.Position, QSkill.Range, Color.magenta);

			if (LastOutput == null)
				return;

			if (DrawAim)
				Drawing.DrawCircle(LocalPlayer.AimPosition, 0.75f, Color.red);
		}

		private void GameOnOnUpdate(EventArgs args)
		{
			if (LocalPlayer.Instance == null)
				return;
			
			if (LocalPlayer.Instance.Living.IsDead ||
			    //!LocalPlayer.Instance.AbilitySystem.CanCastAbilities ||
			    LocalPlayer.Instance.HasCCOfType(CCType.SpellBlock) ||
			    LocalPlayer.Instance.HasCc("PANIC") ||
				LocalPlayer.Instance.Buffs.Any(b => b.IsSpellBlock))
			{
				AbortMission();
				return;
			}

			if (TryRmb())
				return;

			if (AllyKey)
			{
				if (!TargetAlly())
					AbortMission();
				return;
			}

			if (EnemyKey)
			{
				if (!TargetEnemy(CastingE ? ESkill : LmbSkill))
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
			var useq = UseQ && QSkill.IsReady;
			var exqready = Ex2Skill.IsReady;
			var useexq = UseExQ && exqready;

			if (!useq && !useexq)
				return false;

			var casting = false;
			var count = EntitiesManager.EnemyTeam?.Count(e =>
			{
				var ret = e.Distance(LocalPlayer.Instance) <= QSkill.Range * 0.9f &&
				!e.Living.IsDead && !e.PhysicsCollision.IsImmaterial &&
				!e.SpellCollision.IsUnHitable &&
				!e.SpellCollision.IsUnTargetable &&
				!e.Living.IsInvulnerable;

				if (!ret)
					return false;

				if (UseQCasting)
				{
					if (!casting)
						casting = e.AbilitySystem.IsCasting || e.IsChanneling;
				} else
				{
					casting = true;
				}

				return true;
			});

			if (useexq && count >= ExQCount)
			{
				Ex2Skill.Cast();
				StartedCast = true;
				LastOutput = null;

				return true;
			}

			var forceexq = exqready && LocalPlayer.Instance.Living.HealthPercent <= ExQForce;

			if ((useq || forceexq) && count >= QCount && casting)
			{
				if (forceexq)
					Ex2Skill.Cast();
				else
					QSkill.Cast();
				StartedCast = true;
				LastOutput = null;

				return true;
			}

			return false;
		}

		internal static bool TryLmb()
		{
			if (CastingE)
				return TargetEnemy(ESkill) || TargetOrb(ESkill);

			if (!UseLmb)
				return !LmbSkill.IsReady && StartedCast;

			switch ((TargetingOrder) LmbTo.CurrentValue)
			{
				case TargetingOrder.AllyEnemyOrb:
					return TargetAlly() || TargetEnemy(LmbSkill) || TargetOrb(LmbSkill);

				case TargetingOrder.AllyOrbEnemy:
					return TargetAlly() || TargetOrb(LmbSkill) || TargetEnemy(LmbSkill);

				case TargetingOrder.OrbAllyEnemy:
					return TargetOrb(LmbSkill) || TargetAlly() || TargetEnemy(LmbSkill);

				case TargetingOrder.EnemyAllyOrb:
					return TargetEnemy(LmbSkill) || TargetAlly() || TargetOrb(LmbSkill);

				case TargetingOrder.EnemyOrbAlly:
					return TargetEnemy(LmbSkill) || TargetOrb(LmbSkill) || TargetAlly();

				case TargetingOrder.OrbEnemyAlly:
					return TargetOrb(LmbSkill) || TargetEnemy(LmbSkill) || TargetAlly();
			}

			AbortMission();

			return true;
		}

		internal static bool TryRmb()
		{
			try
			{
				if (!UseRmb || !RmbSkill.IsReady)
				{
					LastRmbTarget = null;
					return false;
				}

				if (LastRmbTarget == null || Environment.TickCount - LastRmbRefresh > 250)
				{
					LastRmbTarget = EntitiesManager.LocalTeam?.FirstOrDefault(p =>
					{
						if (p == null || p.Living.IsDead ||
						    p.Living.ImmuneToHeals || p.Living.IsInvulnerable ||
						    !RmbTarget.Get<MenuCheckBox>($"{p.Name}.{p.ObjectName}") ||
						    p.SpellCollision.IsUnHitable || p.SpellCollision.IsUnTargetable ||
						    p.HasBuffOfType(BuffType.Shield))
							return false;

						var buffs = p.Buffs;

						return buffs == null  || 
						buffs.Any(b => b != null &&
						Debuffs.Any(d =>
						{
							var targetName = b.Target?.ObjectName;
							if (string.IsNullOrEmpty(targetName))
								return false;

							return b.ObjectName.EndsWith(d) &&
							targetName == p.ObjectName && 
							RmbMenu.Get<MenuCheckBox>(d) &&
							RmbMenu.Get<MenuSlider>(d + ".hp") > p.Living.HealthPercent;
						}));
					});
					LastRmbRefresh = Environment.TickCount;
				}

				if (LastRmbTarget == null)
					return false;

				/*if (LastRmbTarget.IsLocalPlayer)
					LocalPlayer.PressAbility(AbilitySlot.SelfCastModifier, true);*/

				LocalPlayer.Aim(LastRmbTarget.MapObject.Position);
				RmbSkill.Cast();
				EditingAim = true;
				return true;
			} catch (Exception e)
			{
				Console.WriteLine(e);
				return false;
			}
		}

		internal static bool TargetOrb(SkillBase skill)
		{
			if (!LmbOrb)
				return false;

			var orb = EntitiesManager.CenterOrb;

			if (orb == null || !orb.IsValid || orb.Get<LivingObject>().IsDead ||
			    !(orb.Get<MapGameObject>().Position.Distance(LocalPlayer.Instance) < skill.Range))
				return false;

			LocalPlayer.Aim(orb.Get<MapGameObject>().Position);
			skill.Cast();
			EditingAim = true;
			StartedCast = true;
			return true;
		}

		internal static bool TargetEnemy(SkillBase skill)
		{
			if (!LmbEnemy)
				return false;

			var target =
				EntitiesManager.EnemyTeam?.OrderBy(e => e.Living.Health)
				               .FirstOrDefault(e => ValidateTarget(e) &&
				                                    !(LastOutput = skill.GetPrediction(LocalPlayer.Instance, e)).CollisionResult.IsColliding &&
				                                    LastOutput?.HitChance > HitChance.OutOfRange);

			if (target == null)
				return false;
			
			if (LastOutput == null)
				return false;

			LocalPlayer.Aim(LastOutput.PredictedPosition);
			skill.Cast();
			EditingAim = true;
			StartedCast = true;

			return true;
		}

		internal static bool TargetAlly()
		{
			if (!LmbAlly)
				return false;

			var needHeal = PlayersMenu.Get<MenuCheckBox>($"{LocalPlayer.Instance.Name}.{LocalPlayer.Instance.ObjectName}.Ally") &&
			               CurrentHealthPercent(LocalPlayer.Instance) * 100f < FullHealthCheck;

			var target = EntitiesManager.LocalTeam?.OrderBy(e => e.Living.Health)
			                            .FirstOrDefault(e => !e.IsLocalPlayer &&
			                                                 (needHeal || !LmbHealStop || CurrentHealthPercent(e) * 100f < FullHealthCheck) &&
			                                                 ValidateTarget(e) &&
			                                                 !(LastOutput = LmbSkill.GetPrediction(LocalPlayer.Instance, e)).CollisionResult.IsColliding &&
			                                                 LastOutput.HitChance > HitChance.OutOfRange);

			if (target == null)
				return false;
			
			if (LastOutput == null)
				return false;

			LocalPlayer.Aim(LastOutput.PredictedPosition);
			LmbSkill.Cast();
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
				FullHealthCheck = ComboMenu.Add(new MenuSlider("lmb.fullhealth",
				                                               "Ally has Full Health When Recovery Health Percent is More or Equal to HP%",
				                                               98, 100, 1));
				AllyKey = ComboMenu.Add(new MenuKeybind("ally.key", "LMB On Allies", KeyCode.X));
				EnemyKey = ComboMenu.Add(new MenuKeybind("enemy.key", "LMB On Enemies", KeyCode.V));
				LmbTo = ComboMenu.Add(new MenuComboBox("lmb.to", "Combo Target Order", 0,
				                                       Enum.GetNames(typeof(TargetingOrder))
				                                           .Select(s => InsertBeforeUpperCase(s, " > ")).ToArray()));
				ComboMenu.AddSeparator(10);

				ComboMenu.AddLabel(" - Q Settings");
				UseQ = ComboMenu.Add(new MenuCheckBox("use.q", "Use Q"));
				UseQCasting = ComboMenu.Add(new MenuCheckBox("use.qcasting", "Only Use Q if a Target is Casting", false));
				QCount = ComboMenu.Add(new MenuIntSlider("use.q.count", "Use Q Enemies", 1, 3, 1));
				UseExQ = ComboMenu.Add(new MenuCheckBox("use.qex", "Use EX Q"));
				ExQCount = ComboMenu.Add(new MenuIntSlider("use.qex.count", "Use EX Q Enemies", 2, 3, 1));
				ExQForce = ComboMenu.Add(new MenuSlider("use.qex.force", "Force EX Q HP%", 50, 100));

				PolomaMenu.Add(ComboMenu);

				RmbMenu = new Menu("rmbMenu", "RMB Settings");
				RmbMenu.AddLabel("- RMB Settings");
				UseRmb = RmbMenu.Add(new MenuCheckBox("use.rmb", "Use RMB"));
				RmbMenu.AddSeparator(10);

				RmbMenu.AddLabel(" - Debuffs Settings");

				foreach (var debuff in DebuffsDic)
				{
					RmbMenu.AddSeparator(5);
					RmbMenu.Add(new MenuCheckBox(debuff.Key, InsertBeforeUpperCase(debuff.Key, " "), debuff.Value));
					RmbMenu.Add(new MenuSlider(debuff.Key + ".hp", "Use if Ally Under HP%", 85, 100, 1));
				}

				PolomaMenu.Add(RmbMenu);

				RmbTarget = new Menu("rmb.Targets", "RMB Targets");

				PolomaMenu.Add(RmbTarget);

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
				LmbSkill = new SkillBase(AbilitySlot.Ability1, SkillType.Line, 8.5f, 15f, .2f);
				RmbSkill = new SkillBase(AbilitySlot.Ability2, SkillType.Circle, int.MaxValue, 4, .2f);
				SpaceSkill = new SkillBase(AbilitySlot.Ability3, SkillType.Line, 9f, 4, .2f);
				QSkill = new SkillBase(AbilitySlot.Ability4, SkillType.Circle, 2.5f, int.MaxValue, .2f, 0.1f);
				ESkill = new SkillBase(AbilitySlot.Ability5, SkillType.Line, 9f, 17.5f, .4f);
				FSkill = new SkillBase(AbilitySlot.Ability6, SkillType.Circle, 7f, int.MaxValue, .2f, .25f);
				Ex2Skill = new SkillBase(AbilitySlot.EXAbility2, SkillType.Circle, 2.5f, int.MaxValue, .2f, 0.1f)
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
				StartedCast = LocalPlayer.Instance.AbilitySystem.IsCasting;
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
			if (character == null ||
				character.Living.IsDead ||
				!PlayersMenu.Get<MenuCheckBox>($"{character.Name}.{character.ObjectName}.{character.Team}"))
				return false;

			if (character.IsAlly)
				return !character.Living.ImmuneToHeals;

			var spellCol = character.SpellCollision;

			return !spellCol.IsUnHitable && !spellCol.IsUnTargetable &&
				   !character.PhysicsCollision.IsImmaterial &&
			       !character.HasCCOfType(CCType.Consume) &&
				   !character.HasCCOfType(CCType.Parry) &&
			       !character.HasCCOfType(CCType.Counter) && 
				   !character.Buffs.Any(b => b.IsConsume || b.IsCounter || b.IsReflect) &&
				   !ReflectCc.Any(character.HasCc);
		}

		internal static float CurrentHealthPercent(Character character)
		{
			var living = character.Living;

			return living.Health / living.MaxRecoveryHealth;
		}

		public static string InsertBeforeUpperCase(string str, string toInsert)
		{
			var sb = new StringBuilder();

			var previousChar = char.MinValue;

			foreach (var c in str)
			{
				if (char.IsUpper(c))
					if (sb.Length != 0 && previousChar != ' ')
						foreach (var t in toInsert)
							sb.Append(t);

				sb.Append(c);

				previousChar = c;
			}

			return sb.ToString();
		}
	}
}
