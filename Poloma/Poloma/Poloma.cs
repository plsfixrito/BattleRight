using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BattleRight.Core;
using BattleRight.Core.Enumeration;
using BattleRight.Core.GameObjects;
using BattleRight.Core.GameObjects.Models;
using BattleRight.SDK;
using BattleRight.SDK.Enumeration;
using BattleRight.SDK.UI.Models;
using BattleRight.SDK.UI.Values;
using UnityEngine;

namespace Poloma
{
	public class Poloma : IPlugin
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

		internal Dictionary<string, bool> DebuffsDic = new Dictionary<string, bool>
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
		internal string[] Debuffs = { };
		internal string[] ReflectCc = { "GUST", "BULWARK", "RADIANT SHIELD", "TIME BENDER", "BARBED HUSK" };

		internal bool IsPoloma;
		internal bool EditingAim;
		internal bool CastingE => LocalPlayer.Instance.AbilitySystem.CastingAbilityIndex == 9 ||
		                                 LocalPlayer.Instance.AbilitySystem.CastingAbilityName.Contains("MalevolentSpirit");

		internal SkillBase LmbSkill, RmbSkill, SpaceSkill, QSkill, ESkill, RSkill, Ex1Skill, Ex2Skill, FSkill;
		internal Menu ComboMenu, RmbMenu, RmbTarget, PlayersMenu, DrawMenu;

		internal MenuCheckBox UseLmb, LmbEnemy, LmbAlly, LmbOrb, LmbHealStop, UseRmb, UseQ, UseQCasting, UseExQ, UseE, EOrb,
		                             DrawLmb, DrawQ, DrawE, DrawESafe, DrawAim;

		internal MenuKeybind ComboKey, AllyKey, EnemyKey;
		internal MenuComboBox LmbTo;
		internal MenuSlider ExQForce, FullHealthCheck, ESafeRange, RmbSeconds, RmbDelay;
		internal MenuIntSlider QCount, ExQCount;
		internal PredictionOutput LastOutput;
		internal Color HotPink = new Color(1f, 0.4117647058823529f, 0.7058823529411765f, 1f);

		internal Character LastRmbTarget;
		internal float LastRmbRefresh;

		public void Load()
		{
			Debuffs = DebuffsDic.Keys.ToArray();
			if (!CreateMenu())
				Console.WriteLine("Kappa Poloma: Menu creation failed");
			if (!CreateSkills())
				Console.WriteLine("Kappa Poloma: Skills creation failed");

			//Game.OnMatchStateUpdate += LoadInGame;
			Game.OnMatchStart += LoadInGame;
			Game.OnMatchEnd += delegate
			{
				IsPoloma = false;
				Game.OnUpdate -= GameOnOnUpdate;
				Game.OnDraw -= GameOnOnDraw;
				foreach (var child in PlayersMenu.Children)
					PlayersMenu.RemoveItem(child.Name);
				foreach (var child in RmbTarget.Children)
					RmbTarget.RemoveItem(child.Name);
			};
		}

		private void LoadInGame(EventArgs args)
		{
			IsPoloma = LocalPlayer.Instance != null && LocalPlayer.Instance.ChampionEnum == Champion.Poloma;

			if (!IsPoloma)
				return;

			if (EntitiesManager.LocalTeam != null)
			{
				//PlayersMenu.AddLabel("- Local Team");

				foreach (var player in EntitiesManager.LocalTeam)
				{
					if (player != null)
					{
						var name = player.Name;
						var objName = player.ObjectName;
						PlayersMenu.Add(new MenuCheckBox($"{name}.{objName}.Ally",
						                                 "Heal " + name + " (" + objName + ")"));

						RmbTarget.Add(new MenuCheckBox($"{name}.{objName}",
						                               "Other Side " + $"{name} ({objName})"));
					}
				}

				PlayersMenu.AddSeparator(5);
			}

			if (EntitiesManager.EnemyTeam != null)
			{
				//PlayersMenu.AddLabel("- Enemy Team");
				foreach (var player in EntitiesManager.EnemyTeam)
				{
					if (player != null)
					{
						PlayersMenu.Add(new MenuCheckBox($"{player.Name}.{player.ObjectName}.Enemy",
						                                 "Target " + player.Name + " (" + player.ObjectName + ")"));
					}
				}
			}

			Game.OnUpdate += GameOnOnUpdate;
			Game.OnPreUpdate += GameOnOnDraw;
		}

		public void UnLoad()
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

			if (DrawE)
				Drawing.DrawCircle(LocalPlayer.Instance.MapObject.Position, ESkill.Range, Color.gray);

			if (DrawESafe)
				Drawing.DrawCircle(LocalPlayer.Instance.MapObject.Position, ESafeRange, Color.green);

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

			var castE = CastingE;
			if (TryRmb() && !castE)
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

			if (LocalPlayer.Instance.AbilitySystem.IsCasting || LocalPlayer.Instance.IsChanneling)
			{
				var casting = CastingAbility();

				if (casting == LmbSkill.Slot)
				{
					if (TryLmb())
						return;
					AbortMission(true);
				}
				else if (casting == ESkill.Slot)
				{
					if (TryE())
						return;
					AbortMission(true);
				}else if (casting == Ex2Skill.Slot || casting == QSkill.Slot)
				{
					return;
				}
			}

			if (TryQ() && !castE)
				return;

			if (TryE())
				return;
			
			if (TryLmb())
				return;

			AbortMission();
		}

		internal bool TryQ()
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
				LastOutput = null;

				return true;
			}

			return false;
		}

		internal bool TryLmb()
		{
			if (!UseLmb)
				return !LmbSkill.IsReady || LocalPlayer.Instance.AbilitySystem.IsCasting;

			switch ((TargetingOrder) LmbTo.CurrentValue)
			{
				case TargetingOrder.AllyEnemyOrb:
					return TargetAlly() || (LmbEnemy && TargetEnemy(LmbSkill)) || (LmbOrb && TargetOrb(LmbSkill));

				case TargetingOrder.AllyOrbEnemy:
					return TargetAlly() || (LmbOrb && TargetOrb(LmbSkill)) || (LmbEnemy && TargetEnemy(LmbSkill));

				case TargetingOrder.OrbAllyEnemy:
					return (LmbOrb && TargetOrb(LmbSkill)) || TargetAlly() || (LmbEnemy && TargetEnemy(LmbSkill));

				case TargetingOrder.EnemyAllyOrb:
					return (LmbEnemy && TargetEnemy(LmbSkill)) || TargetAlly() || (LmbOrb && TargetOrb(LmbSkill));

				case TargetingOrder.EnemyOrbAlly:
					return (LmbEnemy && TargetEnemy(LmbSkill)) || (LmbOrb && TargetOrb(LmbSkill)) || TargetAlly();

				case TargetingOrder.OrbEnemyAlly:
					return (LmbOrb && TargetOrb(LmbSkill)) || (LmbEnemy && TargetEnemy(LmbSkill)) || TargetAlly();
			}

			AbortMission();

			return true;
		}

		internal bool TryRmb()
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
							       RmbMenu.Get<MenuSlider>(d + ".hp") > p.Living.HealthPercent &&
							       b.TimeToExpire >= RmbSeconds && (b.Age.Age >= RmbDelay || RmbDelay >= b.Duration);
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

		internal bool TryE()
		{
			if (!UseE)
				return false;

			if (!ESkill.IsReady && !CastingE)
				return false;

			if (LocalPlayer.Instance.CountEnemiesInRange(ESafeRange) > 0)
				return false;
			
			return TargetEnemy(ESkill) || (CastingE && EOrb && TargetOrb(ESkill));
		}

		internal bool TargetOrb(SkillBase skill)
		{
			var orb = EntitiesManager.CenterOrb;

			if (orb == null || !orb.IsValid || orb.Get<LivingObject>().IsDead ||
			    !(orb.Get<MapGameObject>().Position.Distance(LocalPlayer.Instance) < skill.Range))
				return false;

			LocalPlayer.Aim(orb.Get<MapGameObject>().Position);
			skill.Cast();
			EditingAim = true;
			return true;
		}

		internal bool TargetEnemy(SkillBase skill)
		{
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
			return true;
		}

		internal bool TargetAlly()
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
			return true;
		}

		internal bool CreateMenu()
		{
			try
			{
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

				ComboMenu.AddLabel(" - E Settings");
				UseE = ComboMenu.Add(new MenuCheckBox("use.E", "Cast E"));
				ESafeRange = ComboMenu.Add(new MenuSlider("use.e.range", "Safe Distance to Cast E", 3.75f, 8, .1f));
				EOrb = ComboMenu.Add(new MenuCheckBox("use.e.orb", "Target Orb if no Enemy found"));

				ComboMenu.AddSeparator(10);

				Loader.Instance.PolomaMenu.Add(ComboMenu);

				RmbMenu = new Menu("rmbMenu", "RMB Settings");
				RmbMenu.AddLabel("- RMB Settings");
				UseRmb = RmbMenu.Add(new MenuCheckBox("use.rmb", "Use RMB"));
				RmbSeconds = RmbMenu.Add(new MenuSlider("rmb.seconds", "Use RMB Only if The Debuff Will Last More than X Seconds.", .25f, 3f, .01f));
				RmbDelay = RmbMenu.Add(new MenuSlider("rmb.delay", "Delay RMB Cast by X Seconds.", .01f, 5, .01f));
				RmbMenu.AddSeparator(10);

				RmbMenu.AddLabel(" - Debuffs Settings");

				foreach (var debuff in DebuffsDic)
				{
					RmbMenu.AddSeparator(5);
					RmbMenu.Add(new MenuCheckBox(debuff.Key, InsertBeforeUpperCase(debuff.Key, " "), debuff.Value));
					RmbMenu.Add(new MenuSlider(debuff.Key + ".hp", "Use if Ally Under HP%", 80, 100, 1));
				}

				Loader.Instance.PolomaMenu.Add(RmbMenu);

				RmbTarget = new Menu("rmb.Targets", "RMB Targets");

				Loader.Instance.PolomaMenu.Add(RmbTarget);

				PlayersMenu = new Menu("Players", "Targeting");

				Loader.Instance.PolomaMenu.Add(PlayersMenu);

				DrawMenu = new Menu("Drawing", "Drawings");
				DrawLmb = DrawMenu.Add(new MenuCheckBox("draw.lmb", "Draw LMB Range"));
				DrawQ = DrawMenu.Add(new MenuCheckBox("draw.q", "Draw Q/EXQ Range"));
				DrawE = DrawMenu.Add(new MenuCheckBox("draw.e", "Draw E Range"));
				DrawESafe = DrawMenu.Add(new MenuCheckBox("draw.eSafe", "Draw E Safe Range"));
				DrawAim = DrawMenu.Add(new MenuCheckBox("draw.aim", "Draw Current Aiming Position"));

				Loader.Instance.PolomaMenu.Add(DrawMenu);

				return true;
			} catch (Exception e)
			{
				Console.WriteLine(e);

				return false;
			}
		}

		internal bool CreateSkills()
		{
			try
			{
				LmbSkill = new SkillBase(AbilitySlot.Ability1, SkillType.Line, 6.8f, 15f, .25f);
				RmbSkill = new SkillBase(AbilitySlot.Ability2, SkillType.Circle, int.MaxValue, 4, .2f);
				SpaceSkill = new SkillBase(AbilitySlot.Ability3, SkillType.Line, 9f, 4, .2f);
				QSkill = new SkillBase(AbilitySlot.Ability4, SkillType.Circle, 2.5f, int.MaxValue, .2f, 0.1f);
				ESkill = new SkillBase(AbilitySlot.Ability5, SkillType.Line, 9f, 17.5f, .4f);
				FSkill = new SkillBase(AbilitySlot.Ability7, SkillType.Circle, 7f, int.MaxValue, .2f, .25f);
				Ex2Skill = new SkillBase(AbilitySlot.EXAbility2, SkillType.Circle, 2.5f, int.MaxValue, .2f, 0.1f)
				           { GetAbilityHudByName = "SoulDrainAbility" };

				return true;
			} catch (Exception e)
			{
				Console.WriteLine(e);

				return false;
			}
		}

		internal void AbortMission(bool cancel = false)
		{
			if (EditingAim)
			{
				LocalPlayer.EditAimPosition = false;
				EditingAim = false;
			}

			if (LocalPlayer.Instance.AbilitySystem.CastingAbilityIndex == 2 ||
				LocalPlayer.Instance.HasCc("OTHER SIDE"))
				return;

			if (cancel)
			{
				if (!LocalPlayer.Instance.HasCC || !LocalPlayer.Instance.CCName.StartsWith("OTHER"))
					LocalPlayer.PressAbility(AbilitySlot.Interrupt, true);
			}

			LastOutput = null;
		}

		internal bool ValidateTarget(Character character)
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

		internal float CurrentHealthPercent(Character character)
		{
			var living = character.Living;

			return living.Health / living.MaxRecoveryHealth;
		}

		public string InsertBeforeUpperCase(string str, string toInsert)
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

		public AbilitySlot CastingAbility()
		{
			switch(LocalPlayer.Instance.AbilitySystem.CastingAbilityIndex)
			{
				case 0:
					return AbilitySlot.Ability1;
				case 1:
					return AbilitySlot.Ability6;
				case 2:
					return AbilitySlot.Ability2;
				case 3:
					return AbilitySlot.Ability3;
				case 5:
					return AbilitySlot.Ability4;
				case 6:
					return AbilitySlot.EXAbility2;
				case 7:
					return AbilitySlot.EXAbility1;
				case 9:
					return AbilitySlot.Ability5;
				case 10:
					return AbilitySlot.Ability7;
				default:
					return AbilitySlot.Ability1;
			}
		}
	}
}
