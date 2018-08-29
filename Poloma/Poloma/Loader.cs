using System;
using BattleRight.Sandbox;
using BattleRight.SDK.UI;
using BattleRight.SDK.UI.Models;

namespace Poloma
{
    public class Loader : IAddon
    {
        public Menu PolomaMenu;
        public IPlugin LoadedPlugin;
        public static Loader Instance;

        public void OnInit()
        {
            Instance = this;
            PolomaMenu = MainMenu.AddMenu("kappa.Poloma", "Kappa Poloma");
            LoadedPlugin = new Poloma();
            LoadedPlugin.Load();
        }

        public void OnUnload()
        {
            LoadedPlugin.UnLoad();
            GC.SuppressFinalize(this);
        }
    }
}
