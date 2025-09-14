import React from "react";
import styles from "../toolPanel/toolPanel.module.scss";
import { GAME_BINDINGS } from "modBindings";
import { getModule } from "cs2/modding";
import { useValue } from "cs2/api";
import { FloatSliderField } from "cs2/bindings";

const SliderField = getModule<FloatSliderField>(
    "game-ui/editor/widgets/fields/number-slider-field.tsx",
    "FloatSliderField",
);

export const Sides = [
    {
        title: "Start",
        icon: "coui://platter/start.svg",
        id: 2,
    },
    {
        title: "Left",
        icon: "coui://platter/left.svg",
        id: 0,
    },
    {
        title: "Right",
        icon: "coui://platter/right.svg",
        id: 1,
    },
    {
        title: "End",
        icon: "coui://platter/end.svg",
        id: 3,
    },
];

export type OffsetControlProps = Record<string, never>;

export const OffsetControl = (props: OffsetControlProps) => {
    const offsetBinding = useValue(GAME_BINDINGS.RE_OFFSET.binding);

    const handleOffsetChange = (e: number) => {
        GAME_BINDINGS.RE_OFFSET.set(e);
    };

    return (
        <div className={styles.controlsRow}>
            <div className={styles.controlsRowContent}>
                <div className={styles.elevationStepSliderField}>
                    {/* <SliderField
                        label="Offset"
                        value={offsetBinding}
                        min={0}
                        max={10}
                        step={0.1}
                        fractionDigits={1}
                        onChange={handleOffsetChange}></SliderField> */}
                </div>
            </div>
        </div>
    );
};
