import React from "react";
import { Dropdown, FOCUS_AUTO, DropdownToggle, Icon, DropdownItem } from "cs2/ui";
import styles from "../toolPanel/toolPanel.module.scss";
import { GAME_BINDINGS } from "modBindings";
import { c } from "utils/classes";
import { useValue } from "cs2/api";
import { VanillaComponents, VanillaThemes } from "components/vanilla/Components";

export type ZoningControlProps = Record<string, never>;

export const ZoningControl = (props: ZoningControlProps) => {
    const zoneDataBinding = useValue(GAME_BINDINGS.ZONE_DATA.binding);
    const zoneBinding = useValue(GAME_BINDINGS.ZONE.binding);

    const dropDownList = (
        <div>
            {zoneDataBinding.map((zoneData, idx) => (
                <DropdownItem<number>
                    key={idx}
                    theme={{ dropdownItem: VanillaThemes.dropdown.dropdownItem }}
                    focusKey={FOCUS_AUTO}
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

    return (
        <VanillaComponents.Section title="Zoning">
            <div className={c(styles.controlsRowContent, styles.controlsRowWidthDropdown)}>
                <Dropdown
                    focusKey={FOCUS_AUTO}
                    initialFocused={"Test"}
                    alignment="left"
                    theme={{
                        ...VanillaThemes.dropdown,
                        dropdownMenu: styles.dropdownMenu,
                    }}
                    content={dropDownList}>
                    <DropdownToggle className={VanillaThemes.toolButton.button}>
                        <Icon
                            className={c(styles.dropdownZoneIcon, styles.dropdownIcon)}
                            src={`${
                                zoneDataBinding.find((z) => z.index == zoneBinding)?.thumbnail
                            }`}
                        />
                        <div className={styles.dropdownToggleLabel}>
                            {zoneDataBinding.find((z) => z.index == zoneBinding)?.name}
                        </div>
                        <Icon
                            className={c(styles.dropdownToggleIcon, styles.dropdownIcon)}
                            src={"Media/Glyphs/ThickStrokeArrowDown.svg"}
                        />
                    </DropdownToggle>
                </Dropdown>
            </div>
        </VanillaComponents.Section>
    );
};
