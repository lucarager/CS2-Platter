import React from "react";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS } from "gameBindings";
import { VF } from "../vanilla/Components";
import { useLocalization } from "cs2/l10n";
import { SnapMode } from "types";
import { useRenderTracker, useValueWrap } from "../../debug";

export const SnapModeSection = function SnapModeSection() {
    useRenderTracker("ToolPanel/SnapModeSection");
    const snapModes = useValueWrap(GAME_BINDINGS.SNAP_MODES.binding, "SnapModes");
    const { translate } = useLocalization();

    const isEnabled = (mode: SnapMode) => snapModes.includes(mode);

    const allModes = [SnapMode.ZoneSide, SnapMode.RoadSide];
    const areAllEnabled = allModes.every(isEnabled);

    const toggleAll = () => {
        if (areAllEnabled) {
            GAME_BINDINGS.SNAP_MODES.set([]);
        } else {
            GAME_BINDINGS.SNAP_MODES.set(allModes);
        }
    };

    const toggleMode = (mode: SnapMode) => {
        if (mode === SnapMode.None) {
            // "None" clears all modes
            GAME_BINDINGS.SNAP_MODES.set([]);
        } else if (isEnabled(mode)) {
            // Remove mode from array
            GAME_BINDINGS.SNAP_MODES.set(snapModes.filter((m) => m !== mode));
        } else {
            // Add mode to array
            GAME_BINDINGS.SNAP_MODES.set([...snapModes, mode]);
        }
    };

    return (
        <VC.Section
            focusKey={VF.FOCUS_DISABLED}
            title={translate("PlatterMod.UI.SectionTitle.SnapMode")}>
            <VC.ToolButton
                className={VT.toolButton.button}
                src={"Media/Tools/Snap Options/All.svg"}
                onSelect={toggleAll}
                selected={areAllEnabled}
                multiSelect={false}
                disabled={false}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={translate("PlatterMod.UI.Tooltip.SnapModeAll")}
            />
            <VC.ToolButton
                className={VT.toolButton.button}
                src={"Media/Tools/Snap Options/ZoneGrid.svg"}
                onSelect={() => toggleMode(SnapMode.ZoneSide)}
                selected={isEnabled(SnapMode.ZoneSide)}
                multiSelect={true}
                disabled={false}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={translate("PlatterMod.UI.Tooltip.SnapModeRoadSide")}
            />
            <VC.ToolButton
                className={VT.toolButton.button}
                src={"Media/Tools/Snap Options/NetSide.svg"}
                onSelect={() => toggleMode(SnapMode.RoadSide)}
                selected={isEnabled(SnapMode.RoadSide)}
                multiSelect={true}
                disabled={false}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={translate("PlatterMod.UI.Tooltip.SnapModeZoneSide")}
            />
        </VC.Section>
    );
};
