import React, { useRef } from "react";
import {
    Button,
    Portal,
    Panel,
    PanelSection,
    Dropdown,
    FOCUS_AUTO,
    DropdownToggle,
    DropdownItem,
    Icon,
} from "cs2/ui";
import { $bindings, triggers } from "modBindings";
// import { PanelState } from "mit-mainpanel/panelState";

// import icon from "../img/MoveIt_Active.svg";
import styles from "./panel.module.scss";
import { buttonId } from "toolButton/button";
import { VanillaComponentResolver } from "../utils/VanillaComponentResolver";
import { Theme } from "cs2/bindings";
import { getModule } from "cs2/modding";
import { c } from "utils/classes";

const AssetCategoryTabTheme: Theme | any = getModule(
    "game-ui/game/components/asset-menu/asset-category-tab-bar/asset-category-tab-bar.module.scss",
    "classes",
);

const DropdownTheme: Theme | any = getModule(
    "game-ui/common/input/dropdown/dropdown.module.scss",
    "classes",
);

const SliderField: any = getModule(
    "game-ui/editor/widgets/fields/number-slider-field.tsx",
    "FloatSliderField",
);

const panelYPosition = 0.875;
const panelWidth = 500;
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
    const toolEnabledBinding = $bindings.toolEnabled.use();
    const blockWidthBinding = $bindings.blockWidth.use();
    const blockDepthBinding = $bindings.blockDepth.use();
    const toolModeBinding = $bindings.toolMode.use();
    const prefabBinding = $bindings.prefab.use();
    const spacingBinding = $bindings.spacing.use();
    const offsetBinding = $bindings.offset.use();
    const sidesBinding = $bindings.sides.use();
    const zoneBinding = $bindings.zone.use();
    const zoneDataBinding = $bindings.zoneData.use();

    // Panel data
    const panelRef = useRef(null);
    const position = getPanelPosition();
    const digits = 1;

    const handleSpacingChange = (e: number) => {
        $bindings.spacing.set(e);
    };

    const handleOffsetChange = (e: number) => {
        $bindings.offset.set(e);
    };

    const dropDownList = (
        <div>
            {zoneDataBinding.map((zoneData, idx) => (
                <DropdownItem<number>
                    theme={{ dropdownItem: DropdownTheme.dropdownItem }}
                    focusKey={FOCUS_AUTO}
                    value={zoneData.index}
                    closeOnSelect={true}
                    sounds={{ select: "select-item" }}
                    onChange={(value) => $bindings.zone.set(value)}>
                    <Icon className={styles.dropdownIcon} src={zoneData.thumbnail} />{" "}
                    {zoneData.name}
                </DropdownItem>
            ))}
        </div>
    );

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
                            AssetCategoryTabTheme.assetCategoryTabBar,
                            styles.subCategoryContainer,
                        ].join(" ")}>
                        <div className={AssetCategoryTabTheme.items}>
                            {ToolModes.map((toolMode, index) => {
                                return (
                                    <Button
                                        key={index}
                                        className={[
                                            VanillaComponentResolver.instance.assetGridTheme.item,
                                            styles.tabButton,
                                            toolModeBinding === index && styles.selected,
                                        ].join(" ")}
                                        focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED}
                                        onSelect={() => $bindings.toolMode.set(index)}>
                                        {toolMode.title}
                                    </Button>
                                );
                            })}
                        </div>
                    </div>
                    <PanelSection className={styles.section}>
                        <div ref={panelRef}>
                            {/* <div className={styles.prefabPreview}>
                                <img
                                    src={
                                        prefabBinding.thumbnail ??
                                        "coui://uil/Standard/ArrowRight.svg"
                                    }
                                />
                            </div> */}
                            <div className={styles.controlsRow}>
                                <div className={styles.controlsRowTitle}>Zoning</div>
                                <div
                                    className={c(
                                        styles.controlsRowContent,
                                        styles.controlsRowWidthDropdown,
                                    )}>
                                    <Dropdown
                                        focusKey={FOCUS_AUTO}
                                        initialFocused={"Test"}
                                        alignment="left"
                                        theme={{
                                            ...DropdownTheme,
                                            dropdownMenu: styles.dropdownMenu,
                                        }}
                                        content={dropDownList}>
                                        <DropdownToggle
                                            className={
                                                VanillaComponentResolver.instance.toolButtonTheme
                                                    .button
                                            }>
                                            <Icon
                                                className={c(
                                                    styles.dropdownZoneIcon,
                                                    styles.dropdownIcon,
                                                )}
                                                src={`${
                                                    zoneDataBinding.find(
                                                        (z) => z.index == zoneBinding,
                                                    )?.thumbnail
                                                }`}
                                            />
                                            <div className={styles.dropdownToggleLabel}>
                                                {
                                                    zoneDataBinding.find(
                                                        (z) => z.index == zoneBinding,
                                                    )?.name
                                                }
                                            </div>
                                            <Icon
                                                className={c(
                                                    styles.dropdownToggleIcon,
                                                    styles.dropdownIcon,
                                                )}
                                                src={"Media/Glyphs/ThickStrokeArrowDown.svg"}
                                            />
                                        </DropdownToggle>
                                    </Dropdown>
                                </div>
                            </div>
                            <div className={styles.controlsRow}>
                                <div className={styles.controlsRowTitle}>Sides</div>
                                <div className={styles.controlsRowContent}>
                                    {[...Array(4).keys()].map((_, index) => (
                                        <div key={index}>
                                            <VanillaComponentResolver.instance.ToolButton
                                                onSelect={() => {
                                                    console.log("Setting sides:", sidesBinding);

                                                    const newArray = [...sidesBinding];
                                                    newArray[index] = !newArray[index];
                                                    console.log("Setting sides:", newArray);
                                                    $bindings.sides.set(newArray);
                                                }}
                                                selected={sidesBinding[index]}
                                                src="Media/Glyphs/ThickStrokeArrowUp.svg"
                                                focusKey={
                                                    VanillaComponentResolver.instance.FOCUS_DISABLED
                                                }
                                                tooltip="Tooltip"
                                                className={[
                                                    VanillaComponentResolver.instance
                                                        .toolButtonTheme.button,
                                                ].join(" ")}
                                            />
                                        </div>
                                    ))}
                                </div>
                            </div>
                            <div className={styles.controlsRow}>
                                <div className={styles.controlsRowTitle}>Block Width</div>
                                <div className={styles.controlsRowContent}>
                                    <VanillaComponentResolver.instance.ToolButton
                                        onSelect={() =>
                                            triggers.adjustBlockSize("BLOCK_WIDTH_DECREASE")
                                        }
                                        src="Media/Glyphs/ThickStrokeArrowLeft.svg"
                                        focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED}
                                        disabled={blockWidthBinding === 2}
                                        tooltip="Tooltip"
                                        className={[
                                            VanillaComponentResolver.instance.toolButtonTheme
                                                .button,
                                            VanillaComponentResolver.instance.mouseToolOptionsTheme
                                                .startButton,
                                        ].join(" ")}
                                    />
                                    <div
                                        className={[
                                            VanillaComponentResolver.instance.mouseToolOptionsTheme
                                                .numberField,
                                            styles.controlsRowValue,
                                        ].join(" ")}>
                                        {blockWidthBinding}
                                    </div>
                                    <VanillaComponentResolver.instance.ToolButton
                                        onSelect={() =>
                                            triggers.adjustBlockSize("BLOCK_WIDTH_INCREASE")
                                        }
                                        src="Media/Glyphs/ThickStrokeArrowRight.svg"
                                        focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED}
                                        disabled={blockWidthBinding === 6}
                                        tooltip="Tooltip"
                                        className={[
                                            VanillaComponentResolver.instance.toolButtonTheme
                                                .button,
                                            VanillaComponentResolver.instance.mouseToolOptionsTheme
                                                .endButton,
                                        ].join(" ")}
                                    />
                                </div>
                            </div>
                            <div className={styles.controlsRow}>
                                <div className={styles.controlsRowTitle}>Block Depth</div>
                                <div className={styles.controlsRowContent}>
                                    <VanillaComponentResolver.instance.ToolButton
                                        onSelect={() =>
                                            triggers.adjustBlockSize("BLOCK_DEPTH_DECREASE")
                                        }
                                        src="Media/Glyphs/ThickStrokeArrowLeft.svg"
                                        focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED}
                                        disabled={blockDepthBinding === 2}
                                        tooltip="Decrease Depth"
                                        className={[
                                            VanillaComponentResolver.instance.toolButtonTheme
                                                .button,
                                            VanillaComponentResolver.instance.mouseToolOptionsTheme
                                                .startButton,
                                        ].join(" ")}
                                    />
                                    <div
                                        className={[
                                            VanillaComponentResolver.instance.mouseToolOptionsTheme
                                                .numberField,
                                            styles.controlsRowValue,
                                        ].join(" ")}>
                                        {blockDepthBinding}
                                    </div>
                                    <VanillaComponentResolver.instance.ToolButton
                                        onSelect={() =>
                                            triggers.adjustBlockSize("BLOCK_DEPTH_INCREASE")
                                        }
                                        src="Media/Glyphs/ThickStrokeArrowRight.svg"
                                        focusKey={VanillaComponentResolver.instance.FOCUS_DISABLED}
                                        disabled={blockDepthBinding === 6}
                                        tooltip="Increase Depth"
                                        className={[
                                            VanillaComponentResolver.instance.toolButtonTheme
                                                .button,
                                            VanillaComponentResolver.instance.mouseToolOptionsTheme
                                                .endButton,
                                        ].join(" ")}
                                    />
                                </div>
                            </div>
                            <div className={styles.controlsRow}>
                                <div className={styles.controlsRowTitle}>Spacing</div>
                                <div className={styles.controlsRowContent}>
                                    <div className={styles.elevationStepSliderField}>
                                        <SliderField
                                            value={spacingBinding}
                                            min={-10}
                                            max={10}
                                            fractionDigits={digits}
                                            onChange={handleSpacingChange}></SliderField>
                                    </div>
                                </div>
                            </div>
                            <div className={styles.controlsRow}>
                                <div className={styles.controlsRowTitle}>Offset</div>
                                <div className={styles.controlsRowContent}>
                                    <div className={styles.elevationStepSliderField}>
                                        <SliderField
                                            value={offsetBinding}
                                            min={0}
                                            max={10}
                                            fractionDigits={digits}
                                            onChange={handleOffsetChange}></SliderField>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </PanelSection>
                </Panel>
            </Portal>
        </>
    );
};
