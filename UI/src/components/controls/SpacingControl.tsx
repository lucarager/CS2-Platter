import React from "react";
import styles from "../toolPanel/toolPanel.module.scss";
import { GAME_BINDINGS } from "modBindings";
import { useValue } from "cs2/api";

// const SliderField = getModule<FloatSliderField>(
//     "game-ui/editor/widgets/fields/number-slider-field.tsx",
//     "FloatSliderField",
// );

export type SpacingControlProps = Record<string, never>;

export const SpacingControl = (props: SpacingControlProps) => {
    const spacingBinding = useValue(GAME_BINDINGS.RE_SPACING.binding);

    const handleSpacingChange = (e: number) => {
        GAME_BINDINGS.RE_SPACING.set(e);
    };

    return (
        <div className={styles.controlsRow}>
            <div className={styles.controlsRowContent}>
                <div className={styles.elevationStepSliderField}>
                    {/* <SliderField
                        label="Spacing"
                        value={spacingBinding}
                        min={-10}
                        max={10}
                        step={0.1}
                        fractionDigits={1}
                        onChange={handleSpacingChange}></SliderField> */}
                </div>
            </div>
        </div>
    );
};
