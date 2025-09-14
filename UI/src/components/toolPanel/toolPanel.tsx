import React, { useRef } from "react";
import { Portal, Panel, PanelSection } from "cs2/ui";
import { GAME_BINDINGS } from "modBindings";
import { buttonId } from "components/toolButton/toolButton";
import styles from "./toolPanel.module.scss";
import { BlockControl } from "../controls/BlockControl";
import { useValue } from "cs2/api";
import { ZoningControl } from "../controls/ZoningControl";
import { RenderControl } from "../controls/RenderControl";

const panelWidth = 400;
const defaultPanelBottomMargin = 15;
const defaultPanelXPosition = 1030;
const defaultPanelYPosition = 175;

const getPanelPosition = (): { x: number; y: number } => {
    // Cache some elements
    const toolButton = document.getElementById(buttonId);
    const toolbar = document.querySelector("div[class^='toolbar']");
    let panelX = defaultPanelXPosition;
    let panelY = defaultPanelYPosition;

    if (toolButton && toolbar && toolButton.offsetLeft > 0) {
        // Cache some numbers
        const toolbarHeight = toolbar.getBoundingClientRect().height;
        const toolButtonLeftOffset = toolButton.offsetLeft;
        const toolButtonWidth = toolButton.offsetHeight;

        panelX = toolButtonLeftOffset + toolButtonWidth / 2 - panelWidth / 2;
        panelY = toolbarHeight + defaultPanelBottomMargin;
    }

    return {
        x: panelX,
        y: panelY,
    };
};

export const ToolPanel = () => {
    // C# Bindings
    const toolEnabledBinding = useValue(GAME_BINDINGS.TOOL_ENABLED.binding);

    // Panel data
    const panelRef = useRef(null);
    const position = getPanelPosition();

    if (!toolEnabledBinding) return null;

    return (
        <>
            <Portal>
                <Panel
                    className={styles.panel}
                    style={{
                        left: position.x,
                        bottom: position.y,
                        width: panelWidth,
                    }}
                    header={
                        <div className={styles.header}>
                            <span className={styles.headerText}>Platter</span>
                        </div>
                    }>
                    <PanelSection className={styles.section}>
                        <div ref={panelRef}>
                            <ZoningControl />
                            <BlockControl />
                            <RenderControl />
                        </div>
                    </PanelSection>
                </Panel>
            </Portal>
        </>
    );
};
