import React from "react";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS } from "gameBindings";
import { useLocalization } from "cs2/l10n";
import styles from "./toolOptionsPanel.module.scss";
import { VF } from "../vanilla/Components";
import { useRenderTracker, useValueWrap } from "../../debug";
import { c } from "utils/classes";

export const ToolViewmodeSection = function ToolViewmodeSection() {
    useRenderTracker("ToolPanel/ToolViewmodeSection");
    const showZonesBinding = useValueWrap(GAME_BINDINGS.SHOW_ZONES.binding, "ShowZones");
    const showContourBinding = useValueWrap(
        GAME_BINDINGS.SHOW_CONTOUR_LINES.binding,
        "ShowContourLines",
    );
    const { translate } = useLocalization();

    return (
        <VC.Section
            title={translate("PlatterMod.UI.SectionTitle.ViewLayers")}
            focusKey={VF.FOCUS_DISABLED}>
            <VC.ToolButton
                src={"Media/Tools/Net Tool/Grid.svg"}
                onSelect={() => GAME_BINDINGS.SHOW_ZONES.set(!showZonesBinding)}
                selected={showZonesBinding}
                multiSelect={false}
                className={c(VT.toolButton.button, styles.platterToolButton)}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={translate("PlatterMod.UI.Tooltip.ShowZones", "ShowZones")}
            />
            <VC.ToolButton
                src={"Media/Tools/Snap Options/ContourLines.svg"}
                onSelect={() => GAME_BINDINGS.SHOW_CONTOUR_LINES.set(!showContourBinding)}
                selected={showContourBinding}
                multiSelect={false}
                className={c(VT.toolButton.button, styles.platterToolButton)}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={translate("PlatterMod.UI.Tooltip.ShowContourLines", "ShowContourLines")}
            />
        </VC.Section>
    );
};
