// <copyright file="CustomAttribute.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Game.Settings {
    using System.Collections.Generic;
    using Game.Input;

    public class CustomSettingsUIMouseBindingAttribute : SettingsUIMouseBindingAttribute {
        public readonly string defaultKey;
        public readonly bool alt;
        public readonly bool ctrl;
        public readonly bool shift;

        public override string control {
            get {
                return defaultKey;
            }
        }
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
