using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;
using System.Collections.Generic;

namespace Platter
{
    [FileLocation(nameof(Platter))]
    [SettingsUIGroupOrder(kButtonGroup, kToggleGroup, kSliderGroup, kDropdownGroup, kKeybindingGroup)]
    [SettingsUIShowGroupName(kButtonGroup, kToggleGroup, kSliderGroup, kDropdownGroup, kKeybindingGroup)]
    [SettingsUIKeyboardAction(Mod.kVectorActionName, ActionType.Vector2, usages: new string[] { Usages.kMenuUsage, "TestUsage" }, interactions: new string[] { "UIButton" }, processors: new string[] { "ScaleVector2(x=100,y=100)" })]
    [SettingsUIKeyboardAction(Mod.kAxisActionName, ActionType.Axis, usages: new string[] { Usages.kMenuUsage, "TestUsage" }, interactions: new string[] { "UIButton" })]
    [SettingsUIKeyboardAction(Mod.kButtonActionName, ActionType.Button, usages: new string[] { Usages.kMenuUsage, "TestUsage" }, interactions: new string[] { "UIButton" })]
    [SettingsUIGamepadAction(Mod.kButtonActionName, ActionType.Button, usages: new string[] { Usages.kMenuUsage, "TestUsage" }, interactions: new string[] { "UIButton" })]
    [SettingsUIMouseAction(Mod.kButtonActionName, ActionType.Button, usages: new string[] { Usages.kMenuUsage, "TestUsage" }, interactions: new string[] { "UIButton" })]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";

        public const string kButtonGroup = "Button";
        public const string kToggleGroup = "Toggle";
        public const string kSliderGroup = "Slider";
        public const string kDropdownGroup = "Dropdown";
        public const string kKeybindingGroup = "KeyBinding";

        public Setting(IMod mod) : base(mod)
        {

        }

        [SettingsUISection(kSection, kButtonGroup)]
        public bool Button { set { Mod.log.Info("Button clicked"); } }

        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUISection(kSection, kButtonGroup)]
        public bool ButtonWithConfirmation { set { Mod.log.Info("ButtonWithConfirmation clicked"); } }

        [SettingsUISection(kSection, kToggleGroup)]
        public bool Toggle { get; set; }

        [SettingsUISlider(min = 0, max = 100, step = 1, scalarMultiplier = 1, unit = Unit.kDataMegabytes)]
        [SettingsUISection(kSection, kSliderGroup)]
        public int IntSlider { get; set; }

        [SettingsUIDropdown(typeof(Setting), nameof(GetIntDropdownItems))]
        [SettingsUISection(kSection, kDropdownGroup)]
        public int IntDropdown { get; set; }

        [SettingsUISection(kSection, kDropdownGroup)]
        public SomeEnum EnumDropdown { get; set; } = SomeEnum.Value1;

        [SettingsUIKeyboardBinding(BindingKeyboard.Q, Mod.kButtonActionName, shift: true)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding KeyboardBinding { get; set; }

        [SettingsUIMouseBinding(BindingMouse.Forward, Mod.kButtonActionName)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding MouseBinding { get; set; }

        [SettingsUIGamepadBinding(BindingGamepad.Cross, Mod.kButtonActionName)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding GamepadBinding { get; set; }


        [SettingsUIKeyboardBinding(BindingKeyboard.DownArrow, AxisComponent.Negative, Mod.kAxisActionName, shift: true)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding FloatBindingNegative { get; set; }

        [SettingsUIKeyboardBinding(BindingKeyboard.UpArrow, AxisComponent.Positive, Mod.kAxisActionName, shift: true)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding FloatBindingPositive { get; set; }

        [SettingsUIKeyboardBinding(BindingKeyboard.S, Vector2Component.Down, Mod.kVectorActionName, shift: true)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding Vector2BindingDown { get; set; }

        [SettingsUIKeyboardBinding(BindingKeyboard.W, Vector2Component.Up, Mod.kVectorActionName, shift: true)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding Vector2BindingUp { get; set; }

        [SettingsUIKeyboardBinding(BindingKeyboard.A, Vector2Component.Left, Mod.kVectorActionName, shift: true)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding Vector2BindingLeft { get; set; }

        [SettingsUIKeyboardBinding(BindingKeyboard.D, Vector2Component.Right, Mod.kVectorActionName, shift: true)]
        [SettingsUISection(kSection, kKeybindingGroup)]
        public ProxyBinding Vector2BindingRight { get; set; }

        [SettingsUISection(kSection, kKeybindingGroup)]
        public bool ResetBindings
        {
            set
            {
                Mod.log.Info("Reset key bindings");
                ResetKeyBindings();
            }
        }


        public DropdownItem<int>[] GetIntDropdownItems()
        {
            var items = new List<DropdownItem<int>>();

            for (var i = 0; i < 3; i += 1)
            {
                items.Add(new DropdownItem<int>()
                {
                    value = i,
                    displayName = i.ToString(),
                });
            }

            return items.ToArray();
        }

        public override void SetDefaults()
        {
            throw new System.NotImplementedException();
        }

        public enum SomeEnum
        {
            Value1,
            Value2,
            Value3,
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Platter" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kButtonGroup), "Buttons" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kToggleGroup), "Toggle" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kSliderGroup), "Sliders" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kDropdownGroup), "Dropdowns" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kKeybindingGroup), "Key bindings" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Button)), "Button" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Button)), $"Simple single button. It should be bool property with only setter or use [{nameof(SettingsUIButtonAttribute)}] to make button from bool property with setter and getter" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ButtonWithConfirmation)), "Button with confirmation" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ButtonWithConfirmation)), $"Button can show confirmation message. Use [{nameof(SettingsUIConfirmationAttribute)}]" },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.ButtonWithConfirmation)), "is it confirmation text which you want to show here?" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Toggle)), "Toggle" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Toggle)), $"Use bool property with setter and getter to get toggable option" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.IntSlider)), "Int slider" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.IntSlider)), $"Use int property with getter and setter and [{nameof(SettingsUISliderAttribute)}] to get int slider" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.IntDropdown)), "Int dropdown" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.IntDropdown)), $"Use int property with getter and setter and [{nameof(SettingsUIDropdownAttribute)}(typeof(SomeType), nameof(SomeMethod))] to get int dropdown: Method must be static or instance of your setting class with 0 parameters and returns {typeof(DropdownItem<int>).Name}" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnumDropdown)), "Simple enum dropdown" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnumDropdown)), $"Use any enum property with getter and setter to get enum dropdown" },

                { m_Setting.GetEnumValueLocaleID(Setting.SomeEnum.Value1), "Value 1" },
                { m_Setting.GetEnumValueLocaleID(Setting.SomeEnum.Value2), "Value 2" },
                { m_Setting.GetEnumValueLocaleID(Setting.SomeEnum.Value3), "Value 3" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.KeyboardBinding)), "Keyboard binding" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.KeyboardBinding)), $"Keyboard binding of Button input action" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.MouseBinding)), "Mouse binding" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.MouseBinding)), $"Mouse binding of Button input action" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.GamepadBinding)), "Gamepad binding" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.GamepadBinding)), $"Gamepad binding of Button input action" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.FloatBindingNegative)), "Negative binding" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.FloatBindingNegative)), $"Negative component of Axis input action" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.FloatBindingPositive)), "Positive binding" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.FloatBindingPositive)), $"Positive component of Axis input action" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Vector2BindingDown)), "Keyboard binding down" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Vector2BindingDown)), $"Down component of Vector input action" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Vector2BindingUp)), "Keyboard binding up" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Vector2BindingUp)), $"Up component of Vector input action" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Vector2BindingLeft)), "Keyboard binding left" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Vector2BindingLeft)), $"Left component of Vector input action" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Vector2BindingRight)), "Keyboard binding right" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Vector2BindingRight)), $"Right component of Vector input action" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetBindings)), "Reset key bindings" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetBindings)), $"Reset all key bindings of the mod" },

                { m_Setting.GetBindingKeyLocaleID(Mod.kButtonActionName), "Button key" },

                { m_Setting.GetBindingKeyLocaleID(Mod.kAxisActionName, AxisComponent.Negative), "Negative key" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kAxisActionName, AxisComponent.Positive), "Positive key" },

                { m_Setting.GetBindingKeyLocaleID(Mod.kVectorActionName, Vector2Component.Down), "Down key" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kVectorActionName, Vector2Component.Up), "Up key" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kVectorActionName, Vector2Component.Left), "Left key" },
                { m_Setting.GetBindingKeyLocaleID(Mod.kVectorActionName, Vector2Component.Right), "Right key" },

                { m_Setting.GetBindingMapLocaleID(), "Mod settings sample" },
            };
        }

        public void Unload()
        {

        }
    }
}
