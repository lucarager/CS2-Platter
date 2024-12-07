namespace Platter.Systems {
    using Colossal.UI.Binding;
    using Game.UI;
    using Platter.Utils;

    internal partial class UISystem : UISystemBase {
        private PrefixedLogger m_Log;

        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(UISystem));
            m_Log.Debug($"OnCreate()");

            AddBinding(new TriggerBinding(PlatterMod.ModName, "TOGGLE_TOOL_EVENT", ToggleTool));
        }

        /// <summary>
        /// Called from the UI
        /// </summary>
        private void ToggleTool() {
            //ParcelTool.RequestToggle();
        }
    }
}
