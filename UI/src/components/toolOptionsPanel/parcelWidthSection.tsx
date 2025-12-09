import React from "react";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import styles from "./toolOptionsPanel.module.scss";
import { Tooltip } from "cs2/ui";
import { c } from "utils/classes";
import { VF } from "../vanilla/Components";
import { useLocalization } from "cs2/l10n";
import { useRenderTracker, useValueWrap } from "../../debug";

export const ParcelWidthSection = function ParcelWidthSection() {
    useRenderTracker("ToolPanel/ParcelWidthSection");
    const { translate } = useLocalization();
    const blockWidthBinding = useValueWrap(GAME_BINDINGS.BLOCK_WIDTH.binding, "BlockWidth");
    const blockWidthMinBinding = useValueWrap(
        GAME_BINDINGS.BLOCK_WIDTH_MIN.binding,
        "BlockWidthMin",
    );
    const blockWidthMaxBinding = useValueWrap(
        GAME_BINDINGS.BLOCK_WIDTH_MAX.binding,
        "BlockWidthMax",
    );

    return (
        <VC.Section
            focusKey={VF.FOCUS_DISABLED}
            title={translate("PlatterMod.UI.SectionTitle.ParcelWidth")}>
            <VC.ToolButton
                onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_WIDTH_DECREASE")}
                src="Media/Glyphs/ThickStrokeArrowLeft.svg"
                focusKey={VF.FOCUS_DISABLED}
                disabled={blockWidthBinding === blockWidthMinBinding}
                tooltip={translate("PlatterMod.UI.Tooltip.BlockWidthDecrease")}
                className={c(VT.toolButton.button, styles.button, VT.mouseToolOptions.startButton)}
            />
            <Tooltip tooltip={translate("PlatterMod.UI.Tooltip.BlockWidthNumber")}>
                <div className={c(VT.mouseToolOptions.numberField)}>
                    {blockWidthBinding + " " + translate("PlatterMod.UI.Label.ParcelSizeUnit")}
                </div>
            </Tooltip>
            <VC.ToolButton
                onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_WIDTH_INCREASE")}
                src="Media/Glyphs/ThickStrokeArrowRight.svg"
                focusKey={VF.FOCUS_DISABLED}
                disabled={blockWidthBinding === blockWidthMaxBinding}
                tooltip={translate("PlatterMod.UI.Tooltip.BlockWidthIncrease")}
                className={c(VT.toolButton.button, styles.button, VT.mouseToolOptions.endButton)}
            />
        </VC.Section>
    );
};
