import React from "react";
import styles from "../toolPanel/toolPanel.module.scss";
import { GAME_BINDINGS } from "modBindings";
import { useValue } from "cs2/api";
import { VanillaComponents, VanillaFocusKey, VanillaThemes } from "components/vanilla/Components";

export type RenderControlProps = Record<string, never>;

export const RenderControl = (props: RenderControlProps) => {
    const renderParcelBinding = useValue(GAME_BINDINGS.RENDER_PARCELS.binding);

    return (
        <>
            <div className={styles.controlsRow}>
                <div className={styles.controlsRowTitle}>Render Parcels</div>
                <div className={styles.controlsRowContent}>
                    <VanillaComponents.Checkbox
                        onChange={() => GAME_BINDINGS.RENDER_PARCELS.set(!renderParcelBinding)}
                        focusKey={VanillaFocusKey.FOCUS_DISABLED}
                        tooltip="Tooltip"
                        checked={renderParcelBinding}
                        className={[
                            // VanillaComponents.checkbox.locked,
                            VanillaThemes.checkbox.label,
                        ].join(" ")}
                    />
                </div>
            </div>
        </>
    );
};
