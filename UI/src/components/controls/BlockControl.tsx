import React from "react";
import styles from "../toolPanel/toolPanel.module.scss";
import { GAME_BINDINGS, GAME_TRIGGERS } from "modBindings";
import { useValue } from "cs2/api";
import { VanillaComponents, VanillaFocusKey, VanillaThemes } from "components/vanilla/Components";

export type BlockControlProps = Record<string, never>;

export const BlockControl = (props: BlockControlProps) => {
    const blockDepthBinding = useValue(GAME_BINDINGS.BLOCK_DEPTH.binding);
    const blockWidthBinding = useValue(GAME_BINDINGS.BLOCK_WIDTH.binding);

    return (
        <>
            <div className={styles.controlsRow}>
                <div className={styles.controlsRowTitle}>Block Width</div>
                <div className={styles.controlsRowContent}>
                    <VanillaComponents.ToolButton
                        onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_WIDTH_DECREASE")}
                        src="Media/Glyphs/ThickStrokeArrowLeft.svg"
                        focusKey={VanillaFocusKey.FOCUS_DISABLED}
                        disabled={blockWidthBinding === 2}
                        tooltip="Tooltip"
                        className={[
                            VanillaThemes.toolButton.button,
                            VanillaThemes.mouseToolOptions.startButton,
                        ].join(" ")}
                    />
                    <div
                        className={[
                            VanillaThemes.mouseToolOptions.numberField,
                            styles.controlsRowValue,
                        ].join(" ")}>
                        {blockWidthBinding}
                    </div>
                    <VanillaComponents.ToolButton
                        onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_WIDTH_INCREASE")}
                        src="Media/Glyphs/ThickStrokeArrowRight.svg"
                        focusKey={VanillaFocusKey.FOCUS_DISABLED}
                        disabled={blockWidthBinding === 6}
                        tooltip="Tooltip"
                        className={[
                            VanillaThemes.toolButton.button,
                            VanillaThemes.mouseToolOptions.endButton,
                        ].join(" ")}
                    />
                </div>
            </div>
            <div className={styles.controlsRow}>
                <div className={styles.controlsRowTitle}>Block Depth</div>
                <div className={styles.controlsRowContent}>
                    <VanillaComponents.ToolButton
                        onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_DEPTH_DECREASE")}
                        src="Media/Glyphs/ThickStrokeArrowLeft.svg"
                        focusKey={VanillaFocusKey.FOCUS_DISABLED}
                        disabled={blockDepthBinding === 2}
                        tooltip="Decrease Depth"
                        className={[
                            VanillaThemes.toolButton.button,
                            VanillaThemes.mouseToolOptions.startButton,
                        ].join(" ")}
                    />
                    <div
                        className={[
                            VanillaThemes.mouseToolOptions.numberField,
                            styles.controlsRowValue,
                        ].join(" ")}>
                        {blockDepthBinding}
                    </div>
                    <VanillaComponents.ToolButton
                        onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_DEPTH_INCREASE")}
                        src="Media/Glyphs/ThickStrokeArrowRight.svg"
                        focusKey={VanillaFocusKey.FOCUS_DISABLED}
                        disabled={blockDepthBinding === 6}
                        tooltip="Increase Depth"
                        className={[
                            VanillaThemes.toolButton.button,
                            VanillaThemes.mouseToolOptions.endButton,
                        ].join(" ")}
                    />
                </div>
            </div>
        </>
    );
};
