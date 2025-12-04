import React, { useEffect } from "react";
import { ModuleRegistryExtend } from "cs2/modding";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import styles from "./toolOptionsPanel.module.scss";
import { Dropdown, DropdownToggle, Icon, DropdownItem, Tooltip } from "cs2/ui";
import { c } from "utils/classes";
import { VF } from "../vanilla/Components";
import { useLocalization } from "cs2/l10n";
import { SnapMode } from "types";
import { useRenderTracker, useValueWrap } from "../../debug";
import { useRef } from "react";

export type BlockControlProps = Record<string, never>;

export const ToolModes = [
    {
        title: "Plop",
        icon: "",
    },
    {
        title: "Road Mode",
        icon: "",
    },
];

export const PlatterToolOptionsPanel: ModuleRegistryExtend = (Component: any) => {
    const PlatterToolOptionsPanelComponentWrapper = (props: any) => {
        const enabledBinding = useValueWrap(
            GAME_BINDINGS.ENABLE_TOOL_BUTTONS.binding,
            "EnableToolButtons",
        );

        const result: JSX.Element = Component();

        if (enabledBinding) {
            result.props.children?.unshift(<ToolPanel key="Platter/ToolPanel" />);
        }

        return result;
    };

    return PlatterToolOptionsPanelComponentWrapper;
};

const ToolPanel = function ToolPanel() {
    useRenderTracker("ToolPanel/ToolPanel");
    const snapModeBinding = useValueWrap(GAME_BINDINGS.SNAP_MODE.binding, "SnapMode");
    const stylesheet = useRef(document.createElement("style"));
    stylesheet.current.type = "text/css";
    stylesheet.current.innerHTML = `
        .${VT.itemGrid.item.split(" ").join(".")}.selected {
            background-color: rgba(0, 0, 0, 0);
            background-image: linear-gradient(to top left, rgba(255, 213, 210, .5), rgba(253, 189, 203, .5));
        }

        .${VT.assetCategoryTabBar.assetCategoryTabBar} {
            border-bottom-color: rgba(255, 98, 182, 0.69);
        }

        .${VT.assetCategoryTabItem.button}.selected {
            background-image: linear-gradient(to top left, rgba(255, 98, 182, 0.25), rgba(253, 189, 203, .5));
        }
    `;

    useEffect(() => {
        document.head.appendChild(stylesheet.current);

        return () => {
            if (document.head.contains(stylesheet.current)) {
                document.head.removeChild(stylesheet.current);
            }
        };
    }, []);

    return (
        <div className={c(styles.moddedSection)}>
            <PrezoningSection />
            {snapModeBinding != SnapMode.None && <SnapRoadsideSection />}
            <ParcelWidthSection />
            <ParcelDepthSection />
            <SnapModeSection />
            <ToolViewmodeSection />
        </div>
    );
};

const ToolViewmodeSection = function ToolViewmodeSection() {
    useRenderTracker("ToolPanel/ToolViewmodeSection");
    const showZonesBinding = useValueWrap(GAME_BINDINGS.SHOW_ZONES.binding, "ShowZones");
    const showContourBinding = useValueWrap(
        GAME_BINDINGS.SHOW_CONTOUR_LINES.binding,
        "ShowContourLines",
    );
    const { translate } = useLocalization();

    return (
        <VC.Section
            title={translate("PlatterMod.UI.SectionTitle.ViewLayers")}
            focusKey={VF.FOCUS_DISABLED}>
            <VC.ToolButton
                src={"Media/Tools/Net Tool/Grid.svg"}
                onSelect={() => GAME_BINDINGS.SHOW_ZONES.set(!showZonesBinding)}
                selected={showZonesBinding}
                multiSelect={false}
                className={VT.toolButton.button}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={translate("PlatterMod.UI.Tooltip.ShowZones", "ShowZones")}
            />
            <VC.ToolButton
                src={"Media/Tools/Snap Options/ContourLines.svg"}
                onSelect={() => GAME_BINDINGS.SHOW_CONTOUR_LINES.set(!showContourBinding)}
                selected={showContourBinding}
                multiSelect={false}
                className={VT.toolButton.button}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={translate("PlatterMod.UI.Tooltip.ShowContourLines", "ShowContourLines")}
            />
        </VC.Section>
    );
};

const PrezoningSection = function PrezoningSection() {
    useRenderTracker("ToolPanel/PrezoningSection");
    const zoneBinding = useValueWrap(GAME_BINDINGS.ZONE.binding, "Zone");
    const zoneDataBinding = useValueWrap(GAME_BINDINGS.ZONE_DATA.binding, "ZoneData");
    const { translate } = useLocalization();
    const categories = ["None", "Residential", "Commercial", "Industrial", "Office"];
    const zones = categories.map((category) => {
        return zoneDataBinding
            .filter((zoneData) => zoneData.category == category)
            .sort((a, b) => a.name.localeCompare(b.name));
    });

    const dropDownList = (
        <div className={styles.dropdownContent}>
            {categories.map((category, index) => (
                <div className={styles.zoneCategory} key={category}>
                    <div className={styles.dropdownCategory}>
                        {category != "None" ? category : ""}
                    </div>
                    <div className={styles.zoneCategoryZones}>
                        {zones[index].map((zoneData, idx) => (
                            <DropdownItem<number>
                                key={idx}
                                className={styles.dropdownItem}
                                focusKey={VF.FOCUS_DISABLED}
                                value={zoneData.index}
                                closeOnSelect={true}
                                sounds={{ select: "select-item" }}
                                onChange={(value) => GAME_BINDINGS.ZONE.set(value)}>
                                <Icon
                                    className={c(styles.dropdownZoneIcon, styles.dropdownIcon)}
                                    src={zoneData.thumbnail}
                                />
                                <span>
                                    {translate(`Assets.NAME[${zoneData.name}]`, zoneData.name)}
                                </span>
                            </DropdownItem>
                        ))}
                    </div>
                </div>
            ))}
        </div>
    );

    return (
        <VC.Section focusKey={VF.FOCUS_DISABLED} title={translate("PlatterMod.UI.SectionTitle.Prezoning")}>
            <Dropdown
                focusKey={VF.FOCUS_DISABLED}
                initialFocused={"Test"}
                alignment="left"
                theme={{
                    ...VT.dropdown,
                    dropdownMenu: styles.dropdownMenu,
                }}
                content={dropDownList}>
                <DropdownToggle
                    openIconComponent
                    className={c(VT.dropdown.dropdownToggle, styles.dropdownToggle)}>
                    <div className={styles.dropdownToggleInner}>
                        <Icon
                            className={c(styles.dropdownZoneIcon, styles.dropdownIcon)}
                            src={`${
                                zoneDataBinding.find((z) => z.index == zoneBinding)?.thumbnail
                            }`}
                        />
                        <div className={styles.dropdownToggleLabel}>
                            {translate(
                                `Assets.NAME[${zoneDataBinding.find((z) => z.index == zoneBinding)?.name}]`,
                            )}
                        </div>
                        <Icon
                            className={c(styles.dropdownToggleIcon, styles.dropdownIcon)}
                            src={"Media/Glyphs/ThickStrokeArrowDown.svg"}
                        />
                    </div>
                </DropdownToggle>
            </Dropdown>
        </VC.Section>
    );
};

const SnapModeSection = function SnapModeSection() {
    useRenderTracker("ToolPanel/SnapModeSection");
    const snapModeBinding = useValueWrap(GAME_BINDINGS.SNAP_MODE.binding, "SnapMode");
    const { translate } = useLocalization();

    return (
        <VC.Section focusKey={VF.FOCUS_DISABLED} title={translate("PlatterMod.UI.SectionTitle.SnapMode")}>
            <VC.ToolButton
                className={VT.toolButton.button}
                src={"coui://uil/Standard/XClose.svg"}
                onSelect={() => GAME_BINDINGS.SNAP_MODE.set(SnapMode.None)}
                selected={snapModeBinding == SnapMode.None}
                multiSelect={false}
                disabled={false}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={translate("PlatterMod.UI.Tooltip.SnapModeNone")}
            />
            <VC.ToolButton
                className={VT.toolButton.button}
                src={"Media/Tools/Snap Options/ZoneGrid.svg"}
                onSelect={() => GAME_BINDINGS.SNAP_MODE.set(SnapMode.ZoneSide)}
                selected={snapModeBinding == SnapMode.ZoneSide}
                multiSelect={false}
                disabled={false}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={translate("PlatterMod.UI.Tooltip.SnapModeRoadSide")}
            />
            <VC.ToolButton
                className={VT.toolButton.button}
                src={"Media/Tools/Snap Options/NetSide.svg"}
                onSelect={() => GAME_BINDINGS.SNAP_MODE.set(SnapMode.RoadSide)}
                selected={snapModeBinding == SnapMode.RoadSide}
                multiSelect={false}
                disabled={false}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={translate("PlatterMod.UI.Tooltip.SnapModeZoneSide")}
            />
        </VC.Section>
    );
};

const SnapRoadsideSection = function SnapRoadsideSection() {
    useRenderTracker("ToolPanel/SnapRoadsideSection");
    const snapSpacingBinding = useValueWrap(GAME_BINDINGS.SNAP_SPACING.binding, "SnapSpacing");
    const { translate } = useLocalization();

    return (
        <VC.Section focusKey={VF.FOCUS_DISABLED} title={translate("PlatterMod.UI.SectionTitle.SnapSpacing")}>
            <VC.ToolButton
                onSelect={() => GAME_BINDINGS.SNAP_SPACING.set(snapSpacingBinding - 1)}
                src="Media/Glyphs/ThickStrokeArrowLeft.svg"
                focusKey={VF.FOCUS_DISABLED}
                disabled={snapSpacingBinding === 0}
                tooltip={translate("PlatterMod.UI.Tooltip.SnapSpacingDecrease")}
                className={c(VT.toolButton.button, styles.button, VT.mouseToolOptions.startButton)}
            />
            <Tooltip tooltip={translate("PlatterMod.UI.Tooltip.SnapSpacingAmount")}>
                <div className={c(VT.mouseToolOptions.numberField)}>{snapSpacingBinding}m</div>
            </Tooltip>
            <VC.ToolButton
                onSelect={() => GAME_BINDINGS.SNAP_SPACING.set(snapSpacingBinding + 1)}
                src="Media/Glyphs/ThickStrokeArrowRight.svg"
                focusKey={VF.FOCUS_DISABLED}
                disabled={snapSpacingBinding === 15}
                tooltip={translate("PlatterMod.UI.Tooltip.SnapSpacingIncrease")}
                className={c(VT.toolButton.button, styles.button, VT.mouseToolOptions.endButton)}
            />
        </VC.Section>
    );
};

const ParcelWidthSection = function ParcelWidthSection() {
    useRenderTracker("ToolPanel/ParcelWidthSection");
    const { translate } = useLocalization();
    const blockWidthBinding = useValueWrap(GAME_BINDINGS.BLOCK_WIDTH.binding, "BlockWidth");
    const blockWidthMinBinding = useValueWrap(
        GAME_BINDINGS.BLOCK_WIDTH_MIN.binding,
        "BlockWidthMin",
    );
    const blockWidthMaxBinding = useValueWrap(
        GAME_BINDINGS.BLOCK_WIDTH_MAX.binding,
        "BlockWidthMax",
    );

    return (
        <VC.Section focusKey={VF.FOCUS_DISABLED} title={translate("PlatterMod.UI.SectionTitle.ParcelWidth")}>
            <VC.ToolButton
                onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_WIDTH_DECREASE")}
                src="Media/Glyphs/ThickStrokeArrowLeft.svg"
                focusKey={VF.FOCUS_DISABLED}
                disabled={blockWidthBinding === blockWidthMinBinding}
                tooltip={translate("PlatterMod.UI.Tooltip.BlockWidthDecrease")}
                className={c(VT.toolButton.button, styles.button, VT.mouseToolOptions.startButton)}
            />
            <Tooltip tooltip={translate("PlatterMod.UI.Tooltip.BlockWidthNumber")}>
                <div className={c(VT.mouseToolOptions.numberField)}>
                    {blockWidthBinding + " " + translate("PlatterMod.UI.Label.ParcelSizeUnit")}
                </div>
            </Tooltip>
            <VC.ToolButton
                onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_WIDTH_INCREASE")}
                src="Media/Glyphs/ThickStrokeArrowRight.svg"
                focusKey={VF.FOCUS_DISABLED}
                disabled={blockWidthBinding === blockWidthMaxBinding}
                tooltip={translate("PlatterMod.UI.Tooltip.BlockWidthIncrease")}
                className={c(VT.toolButton.button, styles.button, VT.mouseToolOptions.endButton)}
            />
        </VC.Section>
    );
};

const ParcelDepthSection = function ParcelDepthSection() {
    useRenderTracker("ToolPanel/ParcelDepthSection");
    const blockDepthBinding = useValueWrap(GAME_BINDINGS.BLOCK_DEPTH.binding, "BlockDepth");
    const blockDepthMinBinding = useValueWrap(
        GAME_BINDINGS.BLOCK_DEPTH_MIN.binding,
        "BlockDepthMin",
    );
    const blockDepthMaxBinding = useValueWrap(
        GAME_BINDINGS.BLOCK_DEPTH_MAX.binding,
        "BlockDepthMax",
    );

    const { translate } = useLocalization();

    return (
        <VC.Section focusKey={VF.FOCUS_DISABLED} title={translate("PlatterMod.UI.SectionTitle.ParcelDepth")}>
            <VC.ToolButton
                onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_DEPTH_DECREASE")}
                src="Media/Glyphs/ThickStrokeArrowLeft.svg"
                focusKey={VF.FOCUS_DISABLED}
                disabled={blockDepthBinding === blockDepthMinBinding}
                tooltip={translate("PlatterMod.UI.Tooltip.BlockDepthDecrease")}
                className={c(VT.toolButton.button, styles.button, VT.mouseToolOptions.startButton)}
            />
            <Tooltip tooltip={translate("PlatterMod.UI.Tooltip.BlockDepthNumber")}>
                <div className={c(VT.mouseToolOptions.numberField)}>
                    {blockDepthBinding + " " + translate("PlatterMod.UI.Label.ParcelSizeUnit")}
                </div>
            </Tooltip>
            <VC.ToolButton
                onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_DEPTH_INCREASE")}
                src="Media/Glyphs/ThickStrokeArrowRight.svg"
                focusKey={VF.FOCUS_DISABLED}
                disabled={blockDepthBinding === blockDepthMaxBinding}
                tooltip={translate("PlatterMod.UI.Tooltip.BlockDepthIncrease")}
                className={c(VT.toolButton.button, styles.button, VT.mouseToolOptions.endButton)}
            />
        </VC.Section>
    );
};
