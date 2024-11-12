using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Input;
using Game.Modding;
using Game.SceneFlow;
using UnityEngine;

namespace Platter
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(Platter)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        private Setting m_Setting;
        public static ProxyAction m_ButtonAction;
        public static ProxyAction m_AxisAction;
        public static ProxyAction m_VectorAction;

        public const string kButtonActionName = "ButtonBinding";
        public const string kAxisActionName = "FloatBinding";
        public const string kVectorActionName = "Vector2Binding";

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));

            m_Setting.RegisterKeyBindings();

            m_ButtonAction = m_Setting.GetAction(kButtonActionName);
            m_AxisAction = m_Setting.GetAction(kAxisActionName);
            m_VectorAction = m_Setting.GetAction(kVectorActionName);

            m_ButtonAction.shouldBeEnabled = true;
            m_AxisAction.shouldBeEnabled = true;
            m_VectorAction.shouldBeEnabled = true;

            m_ButtonAction.onInteraction += (_, phase) => log.Info($"[{m_ButtonAction.name}] On{phase} {m_ButtonAction.ReadValue<float>()}");
            m_AxisAction.onInteraction += (_, phase) => log.Info($"[{m_AxisAction.name}] On{phase} {m_AxisAction.ReadValue<float>()}");
            m_VectorAction.onInteraction += (_, phase) => log.Info($"[{m_VectorAction.name}] On{phase} {m_VectorAction.ReadValue<Vector2>()}");

            AssetDatabase.global.LoadSettings(nameof(Platter), m_Setting, new Setting(this));
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}
