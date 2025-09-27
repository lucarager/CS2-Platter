// <copyright file="CustomAttribute.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Game.Settings {
    using Game.Input;
    using System.Collections.Generic;

    public class CustomSettingsUIMouseBindingAttribute : SettingsUIMouseBindingAttribute {
        public new readonly string defaultKey;
        public new readonly bool alt;
        public new readonly bool ctrl;
        public new readonly bool shift;

        public override string control => defaultKey;

        public override IEnumerable<string> modifierControls {
            get {
                if (shift) {
                    yield return "<Keyboard>/shift";
                }

                if (ctrl) {
                    yield return "<Keyboard>/ctrl";
                }

                if (alt) {
                    yield return "<Keyboard>/alt";
                }
            }
        }

        public CustomSettingsUIMouseBindingAttribute(string defaultKey, AxisComponent component, string actionName = null, bool alt = false, bool ctrl = false, bool shift = false)
            : base(actionName) {
            this.shift = shift;
            this.alt = alt;
            this.ctrl = ctrl;
            this.defaultKey = defaultKey;
        }
    }
}
