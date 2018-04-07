using System;
using BattleRight.Core;
using BattleRight.SDK.Events;
using BattleRight.SDK.EventsArgs;
using BattleRight.SDK.UI;
using BattleRight.SDK.UI.Models;
using BattleRight.SDK.UI.Values;
using UnityEngine;
using CollisionFlags = BattleRight.Core.Enumeration.CollisionFlags;
using Vector2 = BattleRight.Core.Math.Vector2;

namespace BattleRight.Debugger
{
    class Program
    {
        private static Menu _menu;
        private static MenuCheckBox _draw, _spellCast, _stopCast, _ccStart, _ccUpdate, _ccEnd;
        private static MenuIntSlider _drawLines;
        private static MenuSlider _drawSize;
        static void Main(string[] args)
        {
            var test = Game.Instance;
            CustomEvents.Instance.OnStart += OnStart;
            CustomEvents.Instance.OnDraw += OnDraw;
            CustomEvents.Instance.OnMatchStart += OnMatchStart;
            CustomEvents.Instance.OnMatchEnd += OnMatchEnd;
        }

        private static void OnStart(EventArgs eventArgs)
        {
            Console.WriteLine("Debugger: OnStart");
            Console.WriteLine("create menu ...");
            _menu = new Menu("br.debugger", "Debugger");
            _menu.AddLabel("Collision");
            _draw = _menu.Add(new MenuCheckBox("br.collision", "Debug Collision", false));
            _drawLines = _menu.Add(new MenuIntSlider("br.lines", "Lines Amount", 10, 20, 1));
            _drawSize = _menu.Add(new MenuSlider("br.size", "Lines Size", 0.1f, 5, .1f));

            _menu.AddLabel("Spells");
            _spellCast = _menu.Add(new MenuCheckBox("br.spellCast", "Debug Spell Cast", false));
            _stopCast = _menu.Add(new MenuCheckBox("br.spellStopCast", "Debug Spell Stop Cast", false));

            _menu.AddLabel("Crowd Control");
            _ccStart = _menu.Add(new MenuCheckBox("br.ccstart", "Debug CC Start", false));
            _ccUpdate = _menu.Add(new MenuCheckBox("br.ccupdate", "Debug CC Update", false));
            _ccEnd = _menu.Add(new MenuCheckBox("br.ccend", "Debug CC End", false));

            MainMenu.AddMenu(_menu);
            Console.WriteLine("created menu (" + _menu.Children.Count + ") !");
        }

        private static void OnMatchStart(EventArgs eventArgs)
        {
            Console.WriteLine("OnMatchStart : [BaseTime " + Game.BaseTime + "]");
            SpellDetector.Instance.OnSpellCast += OnSpellCast;
            SpellDetector.Instance.OnSpellStopCast += OnSpellStopCast;
            CrowdControlDetector.Instance.OnCCStart += args =>
            {
                if (_ccStart.CurrentValue)
                    OnCrowdControl(args, "OnCCStart");
            };
            CrowdControlDetector.Instance.OnCCUpdate += args =>
            {
                if (_ccUpdate.CurrentValue)
                    OnCrowdControl(args, "OnCCUpdate");
            };
            CrowdControlDetector.Instance.OnCCEnd += args =>
            {
                if (_ccEnd.CurrentValue)
                    OnCrowdControl(args, "OnCCEnd");
            };
        }

        private static void OnMatchEnd(EventArgs eventArgs)
        {
            Console.WriteLine("OnMatchEnd : [BaseTime " + Game.BaseTime + "]");
        }

        private static void OnCrowdControl(CrowdControlArgs crowControlArgs, string type)
        {
            Console.WriteLine(type + " : [BaseTime " + Game.BaseTime + "]");
            Console.WriteLine("- Owner : " + crowControlArgs.Owner.CharName + "(" + crowControlArgs.Owner.Name + ")");
            Console.WriteLine("- Name : " + crowControlArgs.Name);
            Console.WriteLine("- AnimationName : " + crowControlArgs.AnimationName);
            Console.WriteLine("- PercentLeft : " + crowControlArgs.PercentLeft);
            Console.WriteLine("- TotalDuration : " + crowControlArgs.TotalDuration);
        }

        private static void OnSpellStopCast(SpellStopArgs spellStopArgs)
        {
            if (!_stopCast.CurrentValue)
                return;

            Console.WriteLine("OnSpellStopCast : [BaseTime " + Game.BaseTime + "]");
            Console.WriteLine("- Caster : " + spellStopArgs.Caster.CharName + "(" + spellStopArgs.Caster.Name + ")");
            Console.WriteLine("- Ability.Name : " + spellStopArgs.Ability.Name);
            Console.WriteLine("- AbilityIndex : " + spellStopArgs.AbilityIndex);
            Console.WriteLine("- Interrupted : " + spellStopArgs.Interrupted);
        }

        private static void OnSpellCast(SpellCastArgs spellCastArgs)
        {
            if (!_spellCast.CurrentValue)
                return;

            Console.WriteLine(" OnSpellCast : [BaseTime " + Game.BaseTime + "]");
            Console.WriteLine("- Caster : " + spellCastArgs.Caster.CharName + "(" + spellCastArgs.Caster.Name + ")");
            Console.WriteLine("- AbilityIndex : " + spellCastArgs.AbilityIndex);
            Console.WriteLine("- IsChannel : " + spellCastArgs.IsChannel);
            Console.WriteLine("- IsCounter : " + spellCastArgs.IsCounter);
            var spellManager = spellCastArgs.SpellManager;
            if (spellManager != null)
            {
                if (spellManager.CastingAbility != null)
                {
                    Console.WriteLine("- SpellManager.CastingAbility.Name : " + spellManager.CastingAbility.Name);
                    Console.WriteLine("- SpellManager.CastingAbility.ChargeupMinTime : " + spellManager.CastingAbility.ChargeupMinTime);
                }
                Console.WriteLine("- SpellManager.AbilityIndexLastFrame : " + spellManager.AbilityIndexLastFrame);
                Console.WriteLine("- SpellManager.CastTime : " + spellManager.CastTime);
                Console.WriteLine("- SpellManager.PostCastTime : " + spellManager.PostCastTime);
                Console.WriteLine("- SpellManager.SimulatedCastTime : " + spellManager.SimulatedCastTime);
                Console.WriteLine("- SpellManager.StartCastTime : " + spellManager.StartCastTime);
                Console.WriteLine("- SpellManager.StartPostCastTime : " + spellManager.StartPostCastTime);
                Console.WriteLine("- SpellManager.IsCasting : " + spellManager.IsCasting);
                Console.WriteLine("- SpellManager.IsPostCasting : " + spellManager.IsPostCasting);
            }
        }

        static Color ColColor(Vector2 start, Vector2 end, float radius)
        {
            var col = CollisionSolver.CheckThickLineCollision(start, end, radius, CollisionFlags.Bush);
            var flags = col.CollisionFlags;
            var c = Color.white;
            if (flags.HasFlag(CollisionFlags.Bush))
            {
                c = Color.green;
            }
            else if (flags.HasFlag(CollisionFlags.InvisWalls))
            {
                c = Color.cyan;
            }
            else if (flags.HasFlag(CollisionFlags.HighBlock))
            {
                c = Color.yellow;
            }
            else if (flags.HasFlag(CollisionFlags.LowBlock))
            {
                c = Color.magenta;
            }
            else if (flags.HasFlag(CollisionFlags.NPCBlocker))
            {
                c = Color.blue;
            }
            else if (flags.HasFlag(CollisionFlags.SpawnBlock))
            {
                c = Color.black;
            }
            else if (flags.HasFlag(CollisionFlags.Team1))
            {
                c = Color.red;
            }
            else if (flags.HasFlag(CollisionFlags.Team2))
            {
                c = Color.gray;
            }

            return c;
        }

        private static void OnDraw(EventArgs eventArgs)
        {
            if (!Game.IsInGame)
                return;
            
            if (!_draw.CurrentValue)
                return;

            var lines = _drawLines.CurrentValue;
            var size = _drawSize.CurrentValue;
            var mousePos = Input.mousePosition.ScreenToWorld();
            var startPos = new Vector2(mousePos.X - (short)Math.Floor(lines / 2f), mousePos.Y - (short)Math.Floor(lines / 2f));
            for (float x = startPos.X; x < startPos.X + lines; x += size)
            {
                for (float y = startPos.Y; y < startPos.Y + lines; y += size)
                {
                    var pos1 = new Vector2(x, y);
                    var pos2 = new Vector2(x, y + size);
                    var c = ColColor(pos1, pos2, size);
                    Drawing.DrawLine(pos1, pos2, c);
                    var pos3 = new Vector2(x, y + size);
                    var pos4 = new Vector2(x + size, y + size);
                    c = ColColor(pos3, pos4, size);
                    Drawing.DrawLine(pos3, pos4, c);
                    var pos5 = pos4;
                    var pos6 = new Vector2(x + size, y);
                    c = ColColor(pos5, pos6, size);
                    Drawing.DrawLine(pos5, pos6, c);
                    var pos7 = pos1;
                    var pos8 = pos6;
                    c = ColColor(pos7, pos8, size);
                    Drawing.DrawLine(pos7, pos8, c);
                }
            }
        }
    }
}
