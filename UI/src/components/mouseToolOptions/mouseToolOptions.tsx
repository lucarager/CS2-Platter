import React from "react";
import { ModuleRegistryExtend } from "cs2/modding";
import { VanillaComponents, VanillaThemes } from "components/vanilla/Components";
import { useValue } from "cs2/api";
import { GAME_BINDINGS, GAME_TRIGGERS } from "modBindings";
import styles from "./mouseToolOptions.module.scss";
import { Dropdown, DropdownToggle, Icon, DropdownItem } from "cs2/ui";
import { c } from "utils/classes";
import { VanillaFocusKey } from "../vanilla/Components";

export type BlockControlProps = Record<string, never>;

export const PlatterMouseToolOptionsExtension: ModuleRegistryExtend = (Component) => {
    const ExtendedComponent = (props: any) => {
        const blockDepthBinding = useValue(GAME_BINDINGS.BLOCK_DEPTH.binding);
        const blockWidthBinding = useValue(GAME_BINDINGS.BLOCK_WIDTH.binding);
        const renderParcelBinding = useValue(GAME_BINDINGS.RENDER_PARCELS.binding);
        const zoneDataBinding = useValue(GAME_BINDINGS.ZONE_DATA.binding);
        const zoneBinding = useValue(GAME_BINDINGS.ZONE.binding);
        const enabledBinding = useValue(GAME_BINDINGS.ENABLE_TOOL_BUTTONS.binding);

        const dropDownList = (
            <div>
                {zoneDataBinding.map((zoneData, idx) => (
                    <DropdownItem<number>
                        key={idx}
                        theme={{ dropdownItem: VanillaThemes.dropdown.dropdownItem }}
                        focusKey={VanillaFocusKey.FOCUS_DISABLED}
                        value={zoneData.index}
                        closeOnSelect={true}
                        sounds={{ select: "select-item" }}
                        onChange={(value) => GAME_BINDINGS.ZONE.set(value)}>
                        <Icon className={styles.dropdownIcon} src={zoneData.thumbnail} />{" "}
                        {zoneData.name}
                    </DropdownItem>
                ))}
            </div>
        );

        const { children, ...otherProps } = props || {};

        console.log("dropdown", VanillaThemes.dropdown)

        const Toolbar = (
            <div className={styles.moddedSection}>
                <div className={styles.title}>Platter</div>
                <VanillaComponents.Section title="Zoning">
                    <Dropdown
                        focusKey={VanillaFocusKey.FOCUS_DISABLED}
                        initialFocused={"Test"}
                        alignment="left"
                        theme={{
                            ...VanillaThemes.dropdown,
                            dropdownMenu: styles.dropdownMenu,
                        }}
                        content={dropDownList}>
                        <DropdownToggle
                            openIconComponent
                            className={c(
                                VanillaThemes.dropdown.dropdownToggle,
                                styles.dropdownToggle,
                            )}>
                            <div className={styles.dropdownToggleInner}>
                                <Icon
                                    className={c(styles.dropdownZoneIcon, styles.dropdownIcon)}
                                    src={`${
                                        zoneDataBinding.find((z) => z.index == zoneBinding)
                                            ?.thumbnail
                                    }`}
                                />
                                <div className={styles.dropdownToggleLabel}>
                                    {zoneDataBinding.find((z) => z.index == zoneBinding)?.name}
                                </div>
                                <Icon
                                    className={c(styles.dropdownToggleIcon, styles.dropdownIcon)}
                                    src={"Media/Glyphs/ThickStrokeArrowDown.svg"}
                                />
                            </div>
                        </DropdownToggle>
                    </Dropdown>
                </VanillaComponents.Section>

                <VanillaComponents.Section title="Lot Size">
                    <div className={styles.controlsRow}>
                        <VanillaComponents.ToolButton
                            onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_WIDTH_DECREASE")}
                            src="Media/Glyphs/ThickStrokeArrowLeft.svg"
                            focusKey={VanillaFocusKey.FOCUS_DISABLED}
                            disabled={blockWidthBinding === 2}
                            tooltip="Tooltip"
                            className={c(
                                VanillaThemes.toolButton.button,
                                styles.button,
                                VanillaThemes.mouseToolOptions.startButton,
                            )}
                        />
                        <div
                            className={c(
                                VanillaThemes.mouseToolOptions.numberField,
                                styles.controlsRowValue,
                            )}>
                            {blockWidthBinding}
                        </div>
                        <VanillaComponents.ToolButton
                            onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_WIDTH_INCREASE")}
                            src="Media/Glyphs/ThickStrokeArrowRight.svg"
                            focusKey={VanillaFocusKey.FOCUS_DISABLED}
                            disabled={blockWidthBinding === 6}
                            tooltip="Tooltip"
                            className={c(
                                VanillaThemes.toolButton.button,
                                styles.button,
                                VanillaThemes.mouseToolOptions.endButton,
                            )}
                        />
                    </div>
                    <div className={styles.controlsRow}>
                        <VanillaComponents.ToolButton
                            onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_DEPTH_DECREASE")}
                            src="Media/Glyphs/ThickStrokeArrowLeft.svg"
                            focusKey={VanillaFocusKey.FOCUS_DISABLED}
                            disabled={blockDepthBinding === 2}
                            tooltip="Decrease Depth"
                            className={c(
                                VanillaThemes.toolButton.button,
                                styles.button,
                                VanillaThemes.mouseToolOptions.startButton,
                            )}
                        />
                        <div
                            className={c(
                                VanillaThemes.mouseToolOptions.numberField,
                                styles.controlsRowValue,
                            )}>
                            {blockDepthBinding}
                        </div>
                        <VanillaComponents.ToolButton
                            onSelect={() => GAME_TRIGGERS.ADJUST_BLOCK_SIZE("BLOCK_DEPTH_INCREASE")}
                            src="Media/Glyphs/ThickStrokeArrowRight.svg"
                            focusKey={VanillaFocusKey.FOCUS_DISABLED}
                            disabled={blockDepthBinding === 6}
                            tooltip="Increase Depth"
                            className={c(
                                VanillaThemes.toolButton.button,
                                styles.button,
                                VanillaThemes.mouseToolOptions.endButton,
                            )}
                        />
                    </div>
                </VanillaComponents.Section>

                <VanillaComponents.Section title="Render Settings">
                    <VanillaComponents.Checkbox
                        onChange={() => GAME_BINDINGS.RENDER_PARCELS.set(!renderParcelBinding)}
                        focusKey={VanillaFocusKey.FOCUS_DISABLED}
                        tooltip="Tooltip"
                        checked={renderParcelBinding}
                        className={c(VanillaThemes.checkbox.label)}
                    />
                </VanillaComponents.Section>
            </div>
        );

        return (
            <>
                <Component {...otherProps}>{children}</Component>
                {enabledBinding ? Toolbar : null}
            </>
        );
    };

    return ExtendedComponent;
};
