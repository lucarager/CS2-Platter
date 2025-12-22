import React from "react";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS } from "gameBindings";
import styles from "./toolOptionsPanel.module.scss";
import { Tooltip } from "cs2/ui";
import { c } from "utils/classes";
import { VF } from "../vanilla/Components";
import { useLocalization } from "cs2/l10n";
import { useRenderTracker, useValueWrap } from "../../debug";

export const SnapRoadsideSection = function SnapRoadsideSection() {
    useRenderTracker("ToolPanel/SnapRoadsideSection");
    const snapSpacingBinding = useValueWrap(GAME_BINDINGS.SNAP_SPACING.binding, "SnapSpacing");
    const snapSpacingMaxBinding = useValueWrap(
        GAME_BINDINGS.MAX_SNAP_SPACING.binding,
        "SnapSpacingMax",
    );
    const { translate } = useLocalization();

    return (
        <VC.Section
            focusKey={VF.FOCUS_DISABLED}
            title={translate("PlatterMod.UI.SectionTitle.SnapSpacing")}>
            <VC.ToolButton
                onSelect={() => GAME_BINDINGS.SNAP_SPACING.set(snapSpacingBinding - 1)}
                src="Media/Glyphs/ThickStrokeArrowLeft.svg"
                focusKey={VF.FOCUS_DISABLED}
                disabled={snapSpacingBinding === 0}
                tooltip={translate("PlatterMod.UI.Tooltip.SnapSpacingDecrease")}
                className={c(VT.toolButton.button, styles.button, VT.mouseToolOptions.startButton)}
            />
            <Tooltip tooltip={translate("PlatterMod.UI.Tooltip.SnapSpacingAmount")}>
                <div className={c(VT.mouseToolOptions.numberField)}>{snapSpacingBinding}m</div>
            </Tooltip>
            <VC.ToolButton
                onSelect={() => GAME_BINDINGS.SNAP_SPACING.set(snapSpacingBinding + 1)}
                src="Media/Glyphs/ThickStrokeArrowRight.svg"
                focusKey={VF.FOCUS_DISABLED}
                disabled={snapSpacingBinding === snapSpacingMaxBinding}
                tooltip={translate("PlatterMod.UI.Tooltip.SnapSpacingIncrease")}
                className={c(VT.toolButton.button, styles.button, VT.mouseToolOptions.endButton)}
            />
        </VC.Section>
    );
};
