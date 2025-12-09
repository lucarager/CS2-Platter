import React from "react";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS } from "gameBindings";
import { VF } from "../vanilla/Components";
import { useLocalization } from "cs2/l10n";
import { useRenderTracker, useValueWrap } from "../../debug";

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
                className={VT.toolButton.button}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={translate("PlatterMod.UI.Tooltip.ShowZones", "ShowZones")}
            />
            <VC.ToolButton
                src={"Media/Tools/Snap Options/ContourLines.svg"}
                onSelect={() => GAME_BINDINGS.SHOW_CONTOUR_LINES.set(!showContourBinding)}
                selected={showContourBinding}
                multiSelect={false}
                className={VT.toolButton.button}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={translate("PlatterMod.UI.Tooltip.ShowContourLines", "ShowContourLines")}
            />
        </VC.Section>
    );
};
