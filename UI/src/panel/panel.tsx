import { useRef } from "react";
import { Tooltip, Button, Portal, Panel, PanelSection } from "cs2/ui";
import { bindValue, trigger, useValue } from "cs2/api";
import { bindings, triggers } from "modBindings";
// import { PanelState } from "mit-mainpanel/panelState";

// import icon from "../img/MoveIt_Active.svg";
import styles from "./panel.module.scss";
import { buttonId } from "button/button";

const panelYPosition = 0.875;
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

    console.log(panelX, panelY);

    return {
        x: panelX,
        y: panelY,
    };
};

export const ToolPanel = () => {
    const toolEnabled = useValue(bindings.toolEnabled);
    const blockWidth = useValue(bindings.blockWidth);
    const blockDepth = useValue(bindings.blockDepth);
    const prefab = useValue(bindings.prefab);
    const panelRef = useRef(null);
    const position = getPanelPosition();

    if (!toolEnabled) return null;

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
                    }
                >
                    <PanelSection className={styles.section}>
                        <div ref={panelRef}>
                            <div className={styles.prefabPreview}>
                                <img src="coui://uil/Standard/ArrowRight.svg" />
                            </div>
                            <div className={styles.controlsRow}>
                                <div className={styles.controlsRowTitle}>
                                    Block Width
                                </div>
                                <div className={styles.controlsRowValue}>
                                    2
                                </div>
                                <div className={styles.buttonContainer}>
                                    <Tooltip tooltip={"Tooltip"}>
                                        <Button
                                            className={styles.button}
                                            src="coui://uil/Standard/ArrowLeft.svg"
                                            // focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED}
                                            onSelect={() => triggers.buttonPress("BLOCK_WIDTH_DECREASE")}
                                            variant="icon"
                                        />
                                    </Tooltip>
                                </div>
                                <div className={styles.buttonContainer}>
                                    <Tooltip tooltip={"Tooltip"}>
                                        <Button
                                            className={styles.button}
                                            src="coui://uil/Standard/ArrowRight.svg"
                                            // focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED}
                                            onSelect={() => triggers.buttonPress("BLOCK_WIDTH_INCREASE")}
                                            variant="icon"
                                        />
                                    </Tooltip>
                                </div>
                            </div>
                        </div>
                    </PanelSection>
                </Panel>
            </Portal>
        </>
    );
};
