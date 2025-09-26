namespace Platter.Systems {
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI.Tooltip;
    using Platter.Settings;
    using Unity.Entities;

    /// <summary>
    /// Tooltip System
    /// </summary>
    public partial class PlatterTooltipSystem : TooltipSystemBase {
        private ToolSystem m_ToolSystem;
        private ObjectToolSystem m_ObjectToolSystem;
        private InputHintTooltip m_Tooltip_IncreaseWidth;
        private InputHintTooltip m_Tooltip_DecreaseWidth;
        private InputHintTooltip m_Tooltip_IncreaseDepth;
        private InputHintTooltip m_Tooltip_DecreaseDepth;
        private ProxyAction m_IncreaseWidthAction;
        private ProxyAction m_DecreaseWidthAction;
        private ProxyAction m_IncreaseDepthAction;
        private ProxyAction m_DecreaseDepthAction;

        protected override void OnCreate() {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_ObjectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>();
            m_IncreaseWidthAction = PlatterMod.Instance.ActiveSettings.GetAction(PlatterModSettings.IncreaseParcelWidthActionName);
            m_DecreaseWidthAction = PlatterMod.Instance.ActiveSettings.GetAction(PlatterModSettings.DecreaseParcelWidthActionName);
            m_IncreaseDepthAction = PlatterMod.Instance.ActiveSettings.GetAction(PlatterModSettings.IncreaseParcelDepthActionName);
            m_DecreaseDepthAction = PlatterMod.Instance.ActiveSettings.GetAction(PlatterModSettings.DecreaseParcelDepthActionName);

            m_Tooltip_IncreaseWidth = new InputHintTooltip(m_IncreaseDepthAction, InputManager.DeviceType.Mouse);
            m_Tooltip_DecreaseWidth = new InputHintTooltip(m_DecreaseDepthAction, InputManager.DeviceType.Mouse);
            m_Tooltip_IncreaseDepth = new InputHintTooltip(m_IncreaseWidthAction, InputManager.DeviceType.Mouse);
            m_Tooltip_DecreaseDepth = new InputHintTooltip(m_DecreaseWidthAction, InputManager.DeviceType.Mouse);
        }

        protected override void OnUpdate() {
            if (CurrentlyUsingParcelsInObjectTool()) {
                AddMouseTooltip(m_Tooltip_IncreaseWidth);
                AddMouseTooltip(m_Tooltip_DecreaseWidth);
                AddMouseTooltip(m_Tooltip_IncreaseDepth);
                AddMouseTooltip(m_Tooltip_DecreaseDepth);
            }
        }

        /// <summary>
        /// </summary>
        private bool CurrentlyUsingParcelsInObjectTool() {
            return m_ToolSystem.activeTool is ObjectToolSystem && m_ObjectToolSystem.prefab is ParcelPrefab;
        }
    }
}