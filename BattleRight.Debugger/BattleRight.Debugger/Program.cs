using System;
using System.Linq;
using BattleRight.Core;
using BattleRight.Core.GameObjects;
using BattleRight.Core.GameObjects.Models;
using BattleRight.Helper;
using BattleRight.Sandbox;
using BattleRight.SDK;
using BattleRight.SDK.Events;
using BattleRight.SDK.UI;
using BattleRight.SDK.UI.Models;
using BattleRight.SDK.UI.Values;
using UnityEngine;
using CollisionFlags = BattleRight.Core.Enumeration.CollisionFlags;
using Vector2 = BattleRight.Core.Math.Vector2;

namespace BattleRight.Debugger
{
    public class Program : IAddon
    {
        public Menu Main, Events, Players, Objects, Drawings;
        public MenuCheckBox MatchStart, MatchEnd, ObjectCreate, ObjectDestroy, MatchStateUpdate, Update, Draw, DebugBaseTypes,
                            SpellCast, StopCast, BuffGain, BuffRemove, PrintObject, DrawObject, DebugProjectiles, DebugLine, LineLock, LineCollision,
                            ObjectsCollision, EnemyObjects, AllyObjects, IgnoreDeadObjects, DrawBuffs, DrawCircle, ProjectileBaseTypes;

        public MenuSlider LineWidth, LineRange, CircleRadiusSlider;
        public bool LoadedBaseTypes;
        public string[] ColFlags;

        public void OnInit()
        {
            Main = new Menu("battleright.debugger", "BattleRight Debugger");
            MainMenu.AddMenu(Main);

            Events = Main.Add(new Menu("events", "Events"));
            Events.AddLabel("- Events");
            MatchStart = Events.Add(new MenuCheckBox("MatchStart", "Debug MatchStart", false));
            MatchEnd = Events.Add(new MenuCheckBox("MatchEnd", "Debug MatchEnd", false));
            ObjectCreate = Events.Add(new MenuCheckBox("ObjectCreate", "Debug ObjectCreate", false));
            ObjectCreate.OnValueChange += delegate (ChangedValueArgs<bool> args)
			{
				if (args.NewValue)
					InGameObject.OnCreate += InGameObject_OnCreate;
				else
				    InGameObject.OnCreate -= InGameObject_OnCreate;
			};
			ObjectDestroy = Events.Add(new MenuCheckBox("ObjectDestroy", "Debug ObjectDestroy", false));
            ObjectDestroy.OnValueChange += delegate (ChangedValueArgs<bool> args)
			{
				if (args.NewValue)
					InGameObject.OnDestroy += InGameObject_OnDestroy;
				else
				    InGameObject.OnDestroy -= InGameObject_OnDestroy;
            };
			MatchStateUpdate = Events.Add(new MenuCheckBox("MatchStateUpdate", "Debug MatchStateUpdate", false));
            MatchStateUpdate.OnValueChange += delegate (ChangedValueArgs<bool> args)
            {
                if (args.NewValue)
                    SpellDetector.OnSpellCast += SpellDetector_OnSpellCast;
                else
                    SpellDetector.OnSpellCast -= SpellDetector_OnSpellCast;
            };
            Draw = Events.Add(new MenuCheckBox("Draw", "Debug Draw", false));
            Update = Events.Add(new MenuCheckBox("Update", "Debug Update", false));
            SpellCast = Events.Add(new MenuCheckBox("SpellCast", "Debug SpellCast", false));
            SpellCast.OnValueChange += delegate(ChangedValueArgs<bool> args)
            {
                if(args.NewValue)
                    Game.OnMatchStateUpdate += Game_OnMatchStateUpdate;
                else
                    Game.OnMatchStateUpdate -= Game_OnMatchStateUpdate;
            };
            StopCast = Events.Add(new MenuCheckBox("StopCast", "Debug StopCast", false));
            StopCast.OnValueChange += delegate (ChangedValueArgs<bool> args)
            {
                if (args.NewValue)
                    SpellDetector.OnSpellStopCast += SpellDetector_OnSpellStopCast;
                else
                    SpellDetector.OnSpellStopCast -= SpellDetector_OnSpellStopCast;
            };
            BuffGain = Events.Add(new MenuCheckBox("BuffGain", "Debug BuffGain", false));
            BuffGain.OnValueChange += delegate (ChangedValueArgs<bool> args)
            {
                if (args.NewValue)
                    BuffDetector.OnGainBuff += BuffDetector_OnGainBuff;
                else
                    BuffDetector.OnGainBuff -= BuffDetector_OnGainBuff;
            };
            BuffRemove = Events.Add(new MenuCheckBox("BuffRemove", "Debug BuffRemove", false));
            BuffRemove.OnValueChange += delegate (ChangedValueArgs<bool> args)
            {
                if (args.NewValue)
                    BuffDetector.OnRemoveBuff += BuffDetector_OnRemoveBuff;
                else
                    BuffDetector.OnRemoveBuff -= BuffDetector_OnRemoveBuff;
            };

            Players = Main.Add(new Menu("players", "Players"));
            Players.AddLabel("- Players");
            DrawBuffs = Players.Add(new MenuCheckBox("DrawBuffs", "Draw Players Buffs.", false));
            DebugBaseTypes = Players.Add(new MenuCheckBox("DebugBaseTypes", "Debug Players Base Types and Their States.", false));
			Players.AddLabel("Base Types Will Show Here Once Loaded In Match.");

            Objects = Main.Add(new Menu("objects", "Objects"));
            Objects.AddLabel("- Objects");
            PrintObject = Objects.Add(new MenuCheckBox("print", "Debug Object Near Mouse With Left Click (Console).", false));
            DrawObject = Objects.Add(new MenuCheckBox("draw", "Draw Object States Near Mouse.", false));
            DebugProjectiles = Objects.Add(new MenuCheckBox("DebugProjectiles", "Debug Projectiles (Console).", false));
            ProjectileBaseTypes = Objects.Add(new MenuCheckBox("ProjectileBaseTypes", "Debug Projectiles Base Types (extends from above option).", false));
            DebugProjectiles.OnValueChange += delegate (ChangedValueArgs<bool> args)
            {
                if (args.NewValue)
                    InGameObject.OnCreate += InGameObject_OnCreate1;
                else
                    InGameObject.OnCreate -= InGameObject_OnCreate1;
            };

            Drawings = Main.Add(new Menu("drawings", "Drawings"));
            Drawings.AddLabel("- Drawings");

            Drawings.AddLabel("- Circle");
            DrawCircle = Drawings.Add(new MenuCheckBox("DrawCircle", "Draw Circle On Mouse Position", false));
            CircleRadiusSlider = Drawings.Add(new MenuSlider("CircleRadius", "Circle Radius", 1, 10, .01f));

            Drawings.AddLabel("- Line");
            DebugLine = Drawings.Add(new MenuCheckBox("DebugLine", "Draw Debug Line", false));
            LineLock = Drawings.Add(new MenuCheckBox("LineLock", "Lock Line Range"));
            LineRange = Drawings.Add(new MenuSlider("LineRange", "Debug Line Range", 1, 20, 0.01f));
            LineWidth = Drawings.Add(new MenuSlider("LineWidth", "Debug Line Width", .1f, 10, 0.01f));
            LineCollision = Drawings.Add(new MenuCheckBox("LineCollision", "Line Collision Checks", false));
            ObjectsCollision = Drawings.Add(new MenuCheckBox("ObjectsCollision", "Line Check Objects Collision", false));
            EnemyObjects = Drawings.Add(new MenuCheckBox("EnemyObjects", "Line Ignore Enemy Objects Collision", false));
            AllyObjects = Drawings.Add(new MenuCheckBox("AllyObjects", "Line Ignore Ally Objects Collision"));
            IgnoreDeadObjects = Drawings.Add(new MenuCheckBox("IgnoreDeadObjects", "Ignore Dead Objects", false));

            Drawings.AddSeparator(10);
            Drawings.AddLabel("- Ignore Collisions");
            ColFlags = Enum.GetNames(typeof(CollisionFlags));
            foreach (var flag in ColFlags)
                Drawings.Add(new MenuCheckBox(flag, "Ignore " + flag));

            StartEvents();
        }

        public void StartEvents()
        {
            Game.OnMatchStart += Game_OnMatchStart;
            Game.OnMatchEnd += Game_OnMatchEnd;
            Game.OnDraw += Game_OnDraw;
            Game.OnUpdate += Game_OnUpdate;
        }

        private void Game_OnUpdate(EventArgs args)
        {
            if (Input.GetKeyDown(KeyCode.Mouse0) && PrintObject)
            {
                var nearest = EntitiesManager.InGameObjects.FindAll(x => x.GetStates().Contains("[StateData] Position"))
                                             .OrderBy(x => ((Core.Math.Vector2)x.GetState("Position")).WorldToScreen()
                                                                                                      .Distance(InputManager.MousePosition)).FirstOrDefault();
                Logs.Info(DebugObject(nearest, false, true));
            }
        }

        private void Game_OnDraw(EventArgs args)
        {
            if (!Game.IsInGame)
                return;

            if (PrintObject || DrawObject)
            {
                var nearest = EntitiesManager.InGameObjects.FindAll(x => x.GetStates().Contains("[StateData] Position"))
                                             .OrderBy(x => ((Vector2)x.GetState("Position")).WorldToScreen()
                                                                                                      .Distance(InputManager.MousePosition)).FirstOrDefault();

                if (nearest != null)
                {
                    Drawing.DrawCircle(nearest.Get<MapGameObject>().Position, 1, Color.red);
                    if (DrawObject)
                    {
                        var pos = ((Vector2)nearest.GetState("Position")).WorldToScreen();
                        GUI.Label(new Rect(pos.X, pos.Y, 250, 1000), DebugObject(nearest, false));
                    }
                }
            }

            if (DebugBaseTypes)
            {
                foreach (var player in EntitiesManager.AllPlayers)
                {
                    var pos = player.MapObject.ScreenPosition;
                    GUI.Label(new Rect(pos.X, pos.Y, 500, 1000), DebugObject(player));
                }
            }

            if (DrawBuffs)
            {
                foreach (var player in EntitiesManager.AllPlayers)
                {
                    var pos = player.MapObject.ScreenPosition;
                    var buffs = player.Buffs;
					if(buffs == null)
						continue;
                    var result = buffs.Aggregate("", (current, b) => current + $"Buff ObjectName: {b.ObjectName} - AbilityName: {b.AbilityName}" +
                                                                     $" - Duration: {b.Duration} - TimeToExpire: {b.TimeToExpire}");
                    GUI.Label(new Rect(pos.X, pos.Y, 500, 1000), result);
                }
            }
            
            if (DebugLine)
            {
                var start = LocalPlayer.Instance.MapObject.Position;
                var end = LineLock ? start.Extend(InputManager.MousePosition.ScreenToWorld(), LineRange) : InputManager.MousePosition.ScreenToWorld();

                if (ObjectsCollision)
                {
                    var collideObjects = EntitiesManager.InGameObjects.FindAll(o =>
                    {
                        return o.IsValid && o.GetBaseTypes().Count(x => x == "Living" || x == "SpellCollision" || x == "MapObject" || x == "BaseObject") == 4;
                    });
                    SpellCollisionObject sc = null;
                    MapGameObject mo = null;
                    var colliding = collideObjects.OrderBy(d => d.Get<MapGameObject>().Position.Distance(LocalPlayer.Instance)).FirstOrDefault(o =>
                    {
                        if(IgnoreDeadObjects && o.Get<LivingObject>().IsDead)
                            return false;

                        var teamId = o.Get<BaseGameObject>().TeamId;
                        if ((teamId == LocalPlayer.Instance.BaseObject.TeamId && AllyObjects) ||
                            (teamId != LocalPlayer.Instance.BaseObject.TeamId && EnemyObjects))
                            return false;

                        sc = o.Get<SpellCollisionObject>();
                        mo = o.Get<MapGameObject>();
                        return Geometry.CircleVsThickLine(mo.Position, sc.SpellCollisionRadius, start, end, LineWidth, false);
                    });

                    if (colliding != null)
                        end = start.Extend(end, start.Distance(mo.Position) - ((sc.SpellCollisionRadius + LineWidth) / 2));
                }

                if (LineCollision)
                {
                    var flags = ColFlags.Where(flag => Drawings.Get<MenuCheckBox>(flag))
                                                   .Aggregate<string, CollisionFlags>(0, (current, flag) => current | (CollisionFlags) Enum.Parse(typeof(CollisionFlags), flag));
                    var col = CollisionSolver.CheckThickLineCollision(start, end, LineWidth, flags);

                    if (col.IsColliding)
                    {
                        end = col.CollisionPoint;
                        var sPos = end.WorldToScreen();
                        GUI.Label(new Rect(sPos.X, sPos.Y, 200, 100), col.CollisionFlags.ToString());
                    }
                }
                var rect = CreateRect(start, end, LineWidth);
                for (var i = 0; i <= rect.Length - 1; i++)
                {
                    var nextIndex = (rect.Length - 1 == i) ? 0 : (i + 1);
                    var from = rect[i];
                    var to = rect[nextIndex];
                    Drawing.DrawLine(from, to, Color.white);
                }
            }

            if (DrawCircle)
            {
                Drawing.DrawCircle(InputManager.MousePosition.ScreenToWorld(), CircleRadiusSlider, Color.red);
            }
        }

        private void InGameObject_OnCreate1(InGameObject inGameObject)
        {
            var projectile = inGameObject as Projectile;

			if (projectile == null)
                return;

            if (ProjectileBaseTypes)
            {
                Logs.Info(DebugObject(projectile, false, true));
                return;
            }

            Logs.Info($"== Projectile_OnCreate\n - ObjectName: {projectile.ObjectName}\n - AbilityName: {projectile.AbilityName}" +
                      $"\n - StartPosition: {projectile.StartPosition}\n - LastPosition: {projectile.LastPosition}" +
                      $"\n - CalculatedEndPosition: {projectile.CalculatedEndPosition}\n - Direction: {projectile.Direction}" +
                      $"\n - Range: {projectile.Range}\n - Radius: {projectile.Radius}\n - ChargeupFraction: {projectile.ChargeupFraction}" +
                      $"\n - ReflectTime: {projectile.ReflectTime}\n - Duration: {projectile.Duration}\n - IsReflected: {projectile.IsReflected}" +
                      $"\n - IsConsumable: {projectile.IsConsumable}\n - IsReflectable: {projectile.IsReflectable}\n - IsDuplicate: {projectile.IsDuplicate}" +
                      $"\n - IsMovable: {projectile.IsMovable}\n - IsUnTargetable: {projectile.IsUnTargetable}\n - YieldEnergy: {projectile.YieldEnergy}" +
                      $"\n - YieldLeech: {projectile.YieldLeech}");
        }

        private void InGameObject_OnDestroy(InGameObject inGameObject)
		{
			Logs.Info($"== InGameObject_OnDestroy\n - ObjectName: {inGameObject.ObjectName}\n - Type: {inGameObject.GetType().Name}");
		}

		private void InGameObject_OnCreate(InGameObject inGameObject)
		{
		    Logs.Info($"== InGameObject_OnCreate\n - ObjectName: {inGameObject.ObjectName}\n - Type: {inGameObject.GetType().Name}");
		}
        
        private void Game_OnMatchEnd(EventArgs args)
        {
            if (MatchStart)
                Logs.Info("== Game_OnMatchEnd");
        }

        private void Game_OnMatchStart(EventArgs args)
        {
            if(MatchStart)
                Logs.Info("== Game_OnMatchStart");

            if(LoadedBaseTypes)
                return;

            LoadedBaseTypes = true;
            foreach (var baseType in LocalPlayer.Instance.GetBaseTypes())
                Players.Add(new MenuCheckBox(baseType, "Debug " + baseType, false));
        }

        private void Game_OnMatchStateUpdate(MatchStateUpdate args)
        {
            Logs.Info($"== Game_OnMatchStateUpdate\n - CurrentRound: {args.CurrentRound}\n - GameplayTime: {args.GameplayTime}\n - StateDuration: {args.StateDuration}" +
                      $"\n - TimeInState: {args.TimeInState}\n - NewMatchState: {args.NewMatchState}\n - OldMatchState: {args.OldMatchState}");
        }

        private void BuffDetector_OnRemoveBuff(Character player, Buff buff)
        {
            Logs.Info($"== BuffDetector_OnRemoveBuff\n - Player: {player.Name} ({player.ObjectName})\n - buff ObjectName: {buff.ObjectName}\n - buff AbilityName: {buff.AbilityName}" +
                      $"\n - BuffType: {buff.BuffType}\n - IsTravelBuff: {buff.IsTravelBuff}\n - IsDash: {buff.IsDash}\n - IsSpellBlock: {buff.IsSpellBlock}" +
                      $"\n - IsImmobilize: {buff.IsImmobilize}\n - IsRecastBuff: {buff.IsRecastBuff}");
        }

        private void BuffDetector_OnGainBuff(Character player, Buff buff)
        {
            Logs.Info($"== BuffDetector_OnGainBuff\n - Player: {player.Name} ({player.ObjectName})\n - buff ObjectName: {buff.ObjectName}\n - buff AbilityName: {buff.AbilityName}" +
                      $"\n - BuffType: {buff.BuffType}\n - IsTravelBuff: {buff.IsTravelBuff}\n - IsDash: {buff.IsDash}\n - IsSpellBlock: {buff.IsSpellBlock}" +
                      $"\n - IsImmobilize: {buff.IsImmobilize}\n - IsRecastBuff: {buff.IsRecastBuff}");
        }

        private void SpellDetector_OnSpellStopCast(SDK.EventsArgs.SpellStopArgs args)
        {
            Logs.Info($"== SpellDetector_OnSpellStopCast\n - Caster: {args.Caster.Name} ({args.Caster.ObjectName})\n - Ability Name: {args.Ability.Name}" +
                      $"\n - AbilityIndex: {args.AbilityIndex}\n - Interrupted: {args.Interrupted}");
        }

        private void SpellDetector_OnSpellCast(SDK.EventsArgs.SpellCastArgs args)
        {
            Logs.Info($"== SpellDetector_OnSpellCast\n - Caster: {args.Caster.Name} ({args.Caster.ObjectName})\n - Ability Name: {args.Caster.AbilitySystem.GetAbility(args.AbilityIndex)?.Name}" +
                      $"\n - AbilityIndex: {args.AbilityIndex}\n - IsCounter: {args.IsCounter}\n - IsChannel: {args.IsChannel}");
        }
        
        public string DebugObject(InGameObject obj, bool checks = true, bool forPrint = false)
        {
            var result = "";
            foreach (var baseType in obj.GetBaseTypes())
            {
                if (checks && !Players.Get<MenuCheckBox>(baseType))
                    continue;
                var states = Game.GetStates(baseType);
                result += "\nBaseType: " + baseType + "\n";
                result = states.Select(state => state.Replace("[StateData] ", "")).Aggregate(result, (current, s) => current + (" - " + s + ": " + obj.GetState(s) + "\n"));
            }

            if (forPrint && !string.IsNullOrEmpty(result))
                result = "== PRINTING OBJECT ==\n" + result;

			return result;
        }

        public Vector2[] CreateRect(Vector2 start, Vector2 end, float width)
        {
            var a = end.X - start.X;
            var b = end.Y - start.Y;
            var c = (float)Math.Sqrt(a * a + b * b);
            var d = (width * b / c);
            var e = (width * a / c);
            var x1 = start.X - d;
            var y1 = start.Y + e;
            var x2 = start.X + d;
            var y2 = start.Y - e;
            var x3 = end.X + d;
            var y3 = end.Y - e;
            var x4 = end.X - d;
            var y4 = end.Y + e;
            return new []
                   {
                       new Vector2(x1, y1),
                       new Vector2(x2, y2),
                       new Vector2(x3, y3),
                       new Vector2(x4, y4),
                   };
        }

        public void OnUnload()
        {

        }
    }
}
