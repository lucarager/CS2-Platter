import React from "react";
import { ModuleRegistryExtend } from "cs2/modding";
import { VC, VT } from "components/vanilla/Components";
import { useValue } from "cs2/api";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import styles from "./mouseToolOptions.module.scss";
import { Dropdown, DropdownToggle, Icon, DropdownItem, Tooltip } from "cs2/ui";
import { c } from "utils/classes";
import { VF } from "../vanilla/Components";
import { FocusDisabled } from "cs2/input";
import { useLocalization } from "cs2/l10n";
import { SnapMode } from "types";

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

const ToolModeSection = () => {
    const toolModeBinding = useValue(GAME_BINDINGS.TOOL_MODE.binding);

    return (
        <VC.Section title="Tool Mode">
            <VC.ToolButton
                src={"coui://uil/Standard/ArrowDownTriangleNotch.svg"}
                onSelect={() => GAME_BINDINGS.TOOL_MODE.set(0)}
                selected={toolModeBinding == 0}
                multiSelect={false}
                className={VT.toolButton.button}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={"Plop Mode"}
            />
            <VC.ToolButton
                src={"coui://uil/Standard/Road.svg"}
                multiSelect={false}
                className={VT.toolButton.button}
                focusKey={VF.FOCUS_DISABLED}
                tooltip={"Road Platting Mode is under active development. Stay tuned!"}
            />
        </VC.Section>
    );
};

const PrezoningSection = () => {
    const zoneBinding = useValue(GAME_BINDINGS.ZONE.binding);
    const zoneDataBinding = useValue(GAME_BINDINGS.ZONE_DATA.binding);
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
                                focusKey={VF.FOCUS_AUTO}
                                value={zoneData.index}
                                closeOnSelect={true}
                                sounds={{ select: "select-item" }}
                                onChange={(value) => GAME_BINDINGS.ZONE.set(value)}>
                                <Icon className={styles.dropdownIcon} src={zoneData.thumbnail} />
                                {translate(`Assets.NAME[${zoneData.name}]`, zoneData.name)}
                            </DropdownItem>
                        ))}
                    </div>
                </div>
            ))}
        </div>
    );

    return (
        <VC.Section title={translate("PlatterMod.UI.SectionTitle.Prezoning")}>
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

const SnapModeSection = () => {
    const snapModeBinding = useValue(GAME_BINDINGS.SNAP_MODE.binding);

    const { translate } = useLocalization();

    return (
        <VC.Section title={translate("PlatterMod.UI.SectionTitle.SnapMode")}>
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

const SnapRoadsideSection = () => {
    const snapSpacingBinding = useValue(GAME_BINDINGS.SNAP_SPACING.binding);
    const { translate } = useLocalization();

    return (
        <VC.Section title={translate("PlatterMod.UI.SectionTitle.SnapSpacing")}>
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

const ParcelWidthSection = () => {
    const { translate } = useLocalization();
    const blockWidthBinding = useValue(GAME_BINDINGS.BLOCK_WIDTH.binding);
    const blockWidthMinBinding = useValue(GAME_BINDINGS.BLOCK_WIDTH_MIN.binding);
    const blockWidthMaxBinding = useValue(GAME_BINDINGS.BLOCK_WIDTH_MAX.binding);

    return (
        <VC.Section title={translate("PlatterMod.UI.SectionTitle.ParcelWidth")}>
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

const ParcelDepthSection = () => {
    const blockDepthBinding = useValue(GAME_BINDINGS.BLOCK_DEPTH.binding);
    const blockDepthMinBinding = useValue(GAME_BINDINGS.BLOCK_DEPTH_MIN.binding);
    const blockDepthMaxBinding = useValue(GAME_BINDINGS.BLOCK_DEPTH_MAX.binding);

    const { translate } = useLocalization();

    return (
        <VC.Section title={translate("PlatterMod.UI.SectionTitle.ParcelDepth")}>
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

export const PlatterMouseToolOptions: ModuleRegistryExtend = (Component) => {
    const PlatterMouseToolOptionsComponent = (props: any) => {
        const enabledBinding = useValue(GAME_BINDINGS.ENABLE_TOOL_BUTTONS.binding);
        const snapModeBinding = useValue(GAME_BINDINGS.SNAP_MODE.binding);

        const { translate } = useLocalization();
        const { children, ...otherProps } = props || {};

        const Toolbar = (
            <div className={styles.moddedSection}>
                <div className={styles.moddedSection_Header}>
                    <h1 className={styles.moddedSection_Header_Title}>
                        {translate("PlatterMod.UI.SectionTitle.ParcelControls")}
                    </h1>
                </div>
                <FocusDisabled>
                    <ToolModeSection />
                    <PrezoningSection />
                    <SnapModeSection />
                    {snapModeBinding != SnapMode.None ? <SnapRoadsideSection /> : null}
                    <ParcelWidthSection />
                    <ParcelDepthSection />
                </FocusDisabled>
            </div>
        );

        return (
            <>
                <Component {...otherProps}>{children}</Component>
                {enabledBinding ? Toolbar : null}
            </>
        );
    };

    return PlatterMouseToolOptionsComponent;
};
