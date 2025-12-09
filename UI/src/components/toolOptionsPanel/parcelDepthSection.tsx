import React from "react";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import styles from "./toolOptionsPanel.module.scss";
import { Tooltip } from "cs2/ui";
import { c } from "utils/classes";
import { VF } from "../vanilla/Components";
import { useLocalization } from "cs2/l10n";
import { useRenderTracker, useValueWrap } from "../../debug";

export const ParcelDepthSection = function ParcelDepthSection() {
    useRenderTracker("ToolPanel/ParcelDepthSection");
    const blockDepthBinding = useValueWrap(GAME_BINDINGS.BLOCK_DEPTH.binding, "BlockDepth");
    const blockDepthMinBinding = useValueWrap(
        GAME_BINDINGS.BLOCK_DEPTH_MIN.binding,
        "BlockDepthMin",
    );
    const blockDepthMaxBinding = useValueWrap(
        GAME_BINDINGS.BLOCK_DEPTH_MAX.binding,
        "BlockDepthMax",
    );

    const { translate } = useLocalization();

    return (
        <VC.Section
            focusKey={VF.FOCUS_DISABLED}
            title={translate("PlatterMod.UI.SectionTitle.ParcelDepth")}>
            <VC.ToolButton
                onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_DEPTH_DECREASE")}
                src="Media/Glyphs/ThickStrokeArrowLeft.svg"
                focusKey={VF.FOCUS_DISABLED}
                disabled={blockDepthBinding === blockDepthMinBinding}
                tooltip={translate("PlatterMod.UI.Tooltip.BlockDepthDecrease")}
                className={c(VT.toolButton.button, styles.button, VT.mouseToolOptions.startButton)}
            />
            <Tooltip tooltip={translate("PlatterMod.UI.Tooltip.BlockDepthNumber")}>
                <div className={c(VT.mouseToolOptions.numberField)}>
                    {blockDepthBinding + " " + translate("PlatterMod.UI.Label.ParcelSizeUnit")}
                </div>
            </Tooltip>
            <VC.ToolButton
                onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_DEPTH_INCREASE")}
                src="Media/Glyphs/ThickStrokeArrowRight.svg"
                focusKey={VF.FOCUS_DISABLED}
                disabled={blockDepthBinding === blockDepthMaxBinding}
                tooltip={translate("PlatterMod.UI.Tooltip.BlockDepthIncrease")}
                className={c(VT.toolButton.button, styles.button, VT.mouseToolOptions.endButton)}
            />
        </VC.Section>
    );
};
