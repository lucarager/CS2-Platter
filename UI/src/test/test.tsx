import React from "react";
import { Portal, Panel } from "cs2/ui";
import styles from "./test.module.scss";
import { BezierController } from "./BezierController";
import { $bindings } from "modBindings";

export const Test = () => {
    const toolEnabledBinding = $bindings.EEtoolEnabled.use();

    if (!toolEnabledBinding) return null;

    return (
        <Portal>
            <Panel
                className={styles.panel}
                header={
                    <div className={styles.header}>
                        <span className={styles.headerText}>Road Curve Editor</span>
                    </div>
                }>
                <div
                    id="canvascontainer"
                    style={{
                        border: "1px solid #FFFFFF22",
                    }}>
                    Elevation
                    <BezierController />
                    Curvature
                    <BezierController />
                </div>
            </Panel>
        </Portal>
    );
};
``;
