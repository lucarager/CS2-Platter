import React from "react";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS } from "gameBindings";
import { VF } from "../vanilla/Components";
import { useLocalization } from "cs2/l10n";
import { SnapMode } from "types";
import { useRenderTracker, useValueWrap } from "../../debug";

export const SnapModeSection = function SnapModeSection() {
    useRenderTracker("ToolPanel/SnapModeSection");
    const snapModeBinding = useValueWrap(GAME_BINDINGS.SNAP_MODE.binding, "SnapMode");
    const { translate } = useLocalization();

    return (
        <VC.Section
            focusKey={VF.FOCUS_DISABLED}
            title={translate("PlatterMod.UI.SectionTitle.SnapMode")}>
            <VC.ToolButton
                className={VT.toolButton.button}
                src={"coui://uil/Standard/XClose.svg"}
                onSelect={() => GAME_BINDINGS.SNAP_MODE.set(SnapMode.None)}
                selected={snapModeBinding == SnapMode.None}
                multiSelect={false}
                disabled={false}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={translate("PlatterMod.UI.Tooltip.SnapModeNone")}
            />
            <VC.ToolButton
                className={VT.toolButton.button}
                src={"Media/Tools/Snap Options/ZoneGrid.svg"}
                onSelect={() => GAME_BINDINGS.SNAP_MODE.set(SnapMode.ZoneSide)}
                selected={snapModeBinding == SnapMode.ZoneSide}
                multiSelect={false}
                disabled={false}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={translate("PlatterMod.UI.Tooltip.SnapModeRoadSide")}
            />
            <VC.ToolButton
                className={VT.toolButton.button}
                src={"Media/Tools/Snap Options/NetSide.svg"}
                onSelect={() => GAME_BINDINGS.SNAP_MODE.set(SnapMode.RoadSide)}
                selected={snapModeBinding == SnapMode.RoadSide}
                multiSelect={false}
                disabled={false}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={translate("PlatterMod.UI.Tooltip.SnapModeZoneSide")}
            />
        </VC.Section>
    );
};
