import React, { useRef } from "react";
import { Button, Portal, Panel, PanelSection } from "cs2/ui";
import { GAME_BINDINGS, GAME_TRIGGERS } from "modBindings";
import { buttonId } from "components/toolButton/toolButton";
import { VanillaComponentResolver } from "utils/VanillaComponentResolver";
import { c } from "utils/classes";
import styles from "./toolPanel.module.scss";
import { SidesControl } from "../controls/SidesControl";
import { BlockControl } from "../controls/BlockControl";
import { SpacingControl } from "../controls/SpacingControl";
import { OffsetControl } from "../controls/OffsetControl";
import { useValue } from "cs2/api";
import { VanillaThemes } from "components/vanilla/Components";

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

export const ToolModes = [
    {
        id: "Create",
        title: "Plop",
        icon: "",
    },
    {
        id: "Brush",
        title: "Brush",
        icon: "",
    },
    {
        id: "RoadEdge",
        title: "Road Mode",
        icon: "",
    },
];

export const ToolPanel = () => {
    // C# Bindings
    const toolEnabledBinding = useValue(GAME_BINDINGS.TOOL_ENABLED.binding);
    const toolModeBinding = useValue(GAME_BINDINGS.TOOL_MODE.binding);
    const pointsCountBinding = useValue(GAME_BINDINGS.POINTS_COUNT.binding);

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
                    <div
                        className={[
                            VanillaThemes.assetCategoryTabBar.assetCategoryTabBar,
                            styles.subCategoryContainer,
                        ].join(" ")}>
                        <div className={VanillaThemes.assetCategoryTabBar.items}>
                            {ToolModes.map((toolMode, index) => {
                                return (
                                    <Button
                                        key={index}
                                        className={[
                                            // VanillaComponentResolver.instance.assetGrid.item,
                                            styles.tabButton,
                                            toolModeBinding === index && styles.selected,
                                        ].join(" ")}
                                        focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED}
                                        onSelect={() => GAME_BINDINGS.TOOL_MODE.set(index)}>
                                        {toolMode.title}
                                    </Button>
                                );
                            })}
                        </div>
                    </div>
                    {toolModeBinding === 0 && (
                        <PanelSection className={styles.section}>
                            <div ref={panelRef}>
                                {/* <ZoningControl /> */}
                                <BlockControl />
                            </div>
                        </PanelSection>
                    )}
                    {toolModeBinding === 2 && (
                        <PanelSection className={styles.section}>
                            <div ref={panelRef}>
                                {/* <ZoningControl /> */}
                                <SidesControl />
                                <BlockControl />
                                <SpacingControl />
                                <OffsetControl />
                                <div className={c(styles.controlsRow, styles.validatorSection)}>
                                    <Button
                                        className={styles.buildButton}
                                        focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED}
                                        onSelect={() => GAME_TRIGGERS.REQUEST_APPLY()}>
                                        {`Build ${pointsCountBinding} Parcels`}
                                    </Button>
                                </div>
                            </div>
                        </PanelSection>
                    )}
                </Panel>
            </Portal>
        </>
    );
};
