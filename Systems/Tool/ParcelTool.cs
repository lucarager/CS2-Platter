namespace Platter.Systems {
    using Game.Input;
    using Game.Tools;
    using Platter.Utils;
    using Unity.Entities;
    using Unity.Jobs;

    internal partial class ParcelTool : ObjectToolBaseSystem {
        private PrefixedLogger m_Log;
        private ProxyAction m_ApplyAction;
        private ProxyAction m_SecondaryApplyAction;
        internal Game.Common.RaycastSystem m_RaycastSystem;
        internal JobHandle m_InputDeps;

        public override string toolID => "ParcelTool";

        public override Game.Prefabs.PrefabBase GetPrefab() => null;

        public override bool TrySetPrefab(Game.Prefabs.PrefabBase prefab) => false;

        protected override void OnCreate() {
            // Unless you are overriding the default tool set your tool Enabled to false.
            Enabled = false;

            // Logging
            m_Log = new PrefixedLogger(nameof(ParcelTool));
            m_Log.Debug($"OnCreate()");

            // Get Systems
            m_RaycastSystem = World.GetOrCreateSystemManaged<Game.Common.RaycastSystem>();

            // Input Action for left click/press. May work differently for game pads.
            m_ApplyAction = InputManager.instance.FindAction("Tool", "Apply");

            // Input Action for right click/press. May work differently for game pads.
            m_SecondaryApplyAction = InputManager.instance.FindAction("Tool", "Secondary Apply");

            base.OnCreate();
        }

        // sets up actions or whatever else you want to happen when your tool becomes active.
        protected override void OnStartRunning() {
            m_Log.Debug($"OnStartRunning()");

            m_ApplyAction.shouldBeEnabled = true;
            m_SecondaryApplyAction.shouldBeEnabled = true;
        }

        // cleans up actions or whatever else you want to happen when your tool becomes inactive.
        protected override void OnStopRunning() {
            m_Log.Debug($"OnStopRunning()");

            m_ApplyAction.shouldBeEnabled = false;
            m_SecondaryApplyAction.shouldBeEnabled = false;
        }

        protected override void OnDestroy() {
            m_Log.Debug($"OnDestroy()");

            base.OnDestroy();
        }

        public void RequestEnable() {
            m_Log.Debug($"RequestEnable()");

            if (m_ToolSystem.activeTool != this) {
                m_ToolSystem.selected = Entity.Null;
                m_ToolSystem.activeTool = this;
            }
        }
        public void RequestDisable() {
            m_Log.Debug($"RequestDisable()");

            m_ToolSystem.activeTool = m_DefaultToolSystem;
        }
        public void RequestToggle() {
            m_Log.Debug($"RequestToggle()");

            if (m_ToolSystem.activeTool == this) {
                RequestDisable();
            } else {
                RequestEnable();
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            m_InputDeps = base.OnUpdate(inputDeps);

            return inputDeps;
        }
    }
}
