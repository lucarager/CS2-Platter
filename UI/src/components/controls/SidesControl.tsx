import React from "react";
import styles from "../toolPanel/toolPanel.module.scss";
import { VanillaComponentResolver } from "utils/VanillaComponentResolver";
import { GAME_BINDINGS } from "modBindings";
import { c } from "utils/classes";
import { useValue } from "cs2/api";
import { VanillaFocusKey, VanillaThemes } from "components/vanilla/Components";

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

export type SidesControlProps = Record<string, never>;

export const SidesControl = (props: SidesControlProps) => {
    const sidesBinding = useValue(GAME_BINDINGS.RE_SIDES.binding);

    return (
        <div className={styles.controlsRow}>
            <div className={styles.controlsRowTitle}>Sides</div>
            <div className={styles.controlsRowContent}>
                {Sides.map((side) => (
                    <div key={side.id}>
                        <VanillaComponentResolver.instance.ToolButton
                            onSelect={() => {
                                console.log("Setting sides:", sidesBinding);

                                const newArray = [...sidesBinding];
                                newArray[side.id] = !newArray[side.id];
                                console.log("Setting sides:", newArray);
                                GAME_BINDINGS.RE_SIDES.set(newArray);
                            }}
                            selected={sidesBinding[side.id]}
                            src={side.icon}
                            focusKey={VanillaFocusKey.FOCUS_DISABLED}
                            tooltip={`Toggle Parcels on ${side.title}`}
                            className={c(VanillaThemes.toolButton.button, styles.sidesToggleButton)}
                        />
                    </div>
                ))}
            </div>
        </div>
    );
};
