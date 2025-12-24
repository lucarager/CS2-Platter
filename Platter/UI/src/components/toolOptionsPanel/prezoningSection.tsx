import React, { useState } from "react";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS } from "gameBindings";
import styles from "./toolOptionsPanel.module.scss";
import { Icon, DropdownToggle, Dropdown, DropdownItem$1 } from "cs2/ui";
import { c } from "utils/classes";
import { VF } from "../vanilla/Components";
import { useLocalization } from "cs2/l10n";
import { AssetPackData, ZoneGroupData } from "types";
import { useRenderTracker, useValueWrap } from "../../debug";

type AssetPackFilter = {
    element: AssetPackData;
    selected: boolean;
};

type ZoneGroupFilter = {
    element: ZoneGroupData;
    selected: boolean;
};

export const PrezoningSection = function PrezoningSection() {
    useRenderTracker("ToolPanel/PrezoningSection");
    const zoneBinding = useValueWrap(GAME_BINDINGS.ZONE.binding, "Zone");
    const zoneDataBinding = useValueWrap(GAME_BINDINGS.ZONE_DATA.binding, "ZoneData");
    const assetPackBinding = useValueWrap(GAME_BINDINGS.ASSET_PACK_DATA.binding, "AssetPackData");
    const zoneGroupBinding = useValueWrap(GAME_BINDINGS.ZONE_GROUP_DATA.binding, "ZoneGroupData");
    const { translate } = useLocalization();

    // Filters & binding data for filtering
    const [searchFilter, setSearchFilter] = useState<string>();
    const [assetPacks, setAssetPacks] = useState<AssetPackFilter[]>(
        assetPackBinding.map((pack) => ({ element: pack, selected: false })),
    );
    const [baseGameSelected, setBaseGameSelected] = useState(false);
    const [zoneGroups, setZoneGroups] = useState<ZoneGroupFilter[]>(
        zoneGroupBinding.map((group) => ({ element: group, selected: false })),
    );
    const allPacksSelected = assetPacks.every((p) => p.selected) && baseGameSelected;
    const allGroupsSelected = zoneGroups.every((p) => p.selected);

    // Categories for the list
    const categories = [
        { name: "None", icon: "Media/Editor/Thumbnails/Fallback_Generic.svg" },
        { name: "Residential", icon: "Media/Game/Icons/ZoneResidential.svg" },
        { name: "Commercial", icon: "Media/Game/Icons/ZoneCommercial.svg" },
        { name: "Industrial", icon: "Media/Game/Icons/ZoneIndustrial.svg" },
        { name: "Office", icon: "Media/Game/Icons/ZoneOffice.svg" },
    ];

    /**
     * Toggle selected state for a group
     */
    const toggleGroup = (group: ZoneGroupFilter) => {
        group.selected = !group.selected;
        setZoneGroups([...zoneGroups]);
    };

    /**
     * Toggle selected state for a pack
     */
    const togglePack = (pack: AssetPackFilter) => {
        pack.selected = !pack.selected;
        setAssetPacks([...assetPacks]);
    };

    /**
     * Toggle all groups
     */
    const toggleAllGroups = () => {
        // Select all if not all selected, otherwise deselect all
        const desiredState = !allGroupsSelected;
        zoneGroups.forEach((g) => (g.selected = desiredState));
        setZoneGroups([...zoneGroups]);
    };

    /**
     * Toggle all packs
     */
    const toggleAllPacks = () => {
        // Select all if not all selected, otherwise deselect all
        const desiredState = !allPacksSelected;
        assetPacks.forEach((g) => (g.selected = desiredState));
        setAssetPacks([...assetPacks]);
        setBaseGameSelected(desiredState);
    };

    // Return zones filtered by current filters
    const zones = categories.map((category) => {
        return zoneDataBinding
            .filter((zoneData) => {
                const isBaseGame = zoneData.assetPacks.length === 0;

                if (zoneData.areaType != category.name) return false;

                // Always include Unzoned
                if (zoneData.name === "Unzoned") return true;

                // Filter by search query
                if (searchFilter && searchFilter.trim().length > 0) {
                    const localizedName = translate(`Assets.NAME[${zoneData.name}]`, zoneData.name);
                    if (!localizedName?.toLowerCase().includes(searchFilter.toLowerCase())) {
                        return false;
                    }
                }

                // Filter by selected groups
                const selectedGroups = zoneGroups.filter((g) => g.selected).map((g) => g.element);
                if (selectedGroups.length > 0) {
                    const belongsToGroup = selectedGroups.some(
                        (group) => group.entity.index === zoneData.group.index,
                    );
                    if (!belongsToGroup) return false;
                }

                // Filter by selected packs
                const selectedPacks = assetPacks.filter((p) => p.selected).map((p) => p.element);
                if (selectedPacks.length > 0 || baseGameSelected) {
                    let keep = false;

                    // Keep if base game and base game is selected
                    if (baseGameSelected && isBaseGame) keep = true;

                    // Otherwise, keep if any selected pack matches
                    if (!keep && selectedPacks.length > 0) {
                        keep = zoneData.assetPacks.some((zonePack) =>
                            selectedPacks.some(
                                (selectedPack) => selectedPack.entity.index === zonePack.index,
                            ),
                        );
                    }

                    if (!keep) return false;
                }

                // If we're here, it means we should include it
                return true;
            })
            .sort((a, b) => a.name.localeCompare(b.name));
    });

    const DropdownContent = (
        <div className={styles.dropdownContent}>
            {/* Left Sidebar */}
            <div className={styles.dropdownContent__sidebar}>
                <VC.Scrollable>
                    <div className={styles.dropdownContent__sidebar__inner}>
                        {/* Search Filter */}
                        <div className={styles.dropdownContent__sidebar__row}>
                            <div className={styles.dropdownContent__sidebar__row__label}>
                                {translate("PlatterMod.UI.Label.Search", "Search")}
                            </div>
                            <VC.TextInput
                                multiline={1}
                                disabled={false}
                                onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                                    setSearchFilter(e?.target?.value)
                                }
                                type="text"
                                value={searchFilter}
                                className={c(VT.textInput.input, styles.textInput)}
                                focusKey={VF.FOCUS_DISABLED}
                            />
                        </div>
                        {/* Group Filter */}
                        <div className={styles.dropdownContent__sidebar__row}>
                            <div className={styles.dropdownContent__sidebar__row__label}>
                                {translate("PlatterMod.UI.SectionTitle.Category", "Category")}
                            </div>
                            <div className={styles.dropdownContent__sidebar__row__buttons}>
                                <VC.ToolButton
                                    className={c(VT.toolButton.button, styles.filterButton)}
                                    src={"Media/Tools/Snap Options/All.svg"}
                                    multiSelect={true}
                                    selected={allGroupsSelected}
                                    onSelect={toggleAllGroups}
                                    disabled={false}
                                    focusKey={VF.FOCUS_DISABLED}
                                    tooltip={"All"}
                                />
                                {zoneGroups.map((group, index) => (
                                    <VC.ToolButton
                                        key={index}
                                        className={c(VT.toolButton.button, styles.filterButton)}
                                        // Temporary hardcoded icons for zone categories
                                        src={group.element.icon}
                                        multiSelect={true}
                                        selected={group.selected}
                                        onSelect={() => toggleGroup(group)}
                                        disabled={false}
                                        focusKey={VF.FOCUS_DISABLED}
                                        tooltip={group.element.name}
                                    />
                                ))}
                            </div>
                        </div>
                        {/* Pack Filter */}
                        <div className={styles.dropdownContent__sidebar__row}>
                            <div className={styles.dropdownContent__sidebar__row__label}>
                                {translate("PlatterMod.UI.SectionTitle.AssetPacks", "Asset Packs")}
                            </div>
                            <div className={styles.dropdownContent__sidebar__row__buttons}>
                                <VC.ToolButton
                                    className={c(VT.toolButton.button, styles.filterButton)}
                                    src={"Media/Tools/Snap Options/All.svg"}
                                    multiSelect={true}
                                    selected={allPacksSelected}
                                    onSelect={toggleAllPacks}
                                    disabled={false}
                                    focusKey={VF.FOCUS_DISABLED}
                                    tooltip={"All"}
                                />
                                <VC.ToolButton
                                    className={c(VT.toolButton.button, styles.filterButton)}
                                    src={"coui://platter/BaseGame.svg"}
                                    multiSelect={true}
                                    selected={baseGameSelected}
                                    onSelect={() => setBaseGameSelected(!baseGameSelected)}
                                    disabled={false}
                                    focusKey={VF.FOCUS_DISABLED}
                                    tooltip={"Base Game"}
                                />
                                {assetPacks.map((pack, index) => (
                                    <VC.ToolButton
                                        key={index}
                                        className={c(VT.toolButton.button, styles.filterButton)}
                                        src={pack.element.icon}
                                        multiSelect={true}
                                        selected={pack.selected}
                                        onSelect={() => togglePack(pack)}
                                        disabled={false}
                                        focusKey={VF.FOCUS_DISABLED}
                                        tooltip={pack.element.name}
                                    />
                                ))}
                            </div>
                        </div>
                        {/* ...existing code... */}
                    </div>
                </VC.Scrollable>
            </div>
            {/* Right Content */}
            <div className={styles.dropdownContent__content}>
                <VC.Scrollable>
                    <div className={styles.dropdownContent__content__inner}>
                        {categories.map((category, index) => {
                            return (
                                <div className={styles.zoneCategory} key={category.name}>
                                    <div className={styles.dropdownCategory}>
                                        {category.name != "None" ? category.name : ""}
                                    </div>
                                    <div className={styles.zoneCategoryZones}>
                                        {zones[index].length === 0 && (
                                            <span className={styles.noZonesMessage}>
                                                {translate(
                                                    "PlatterMod.UI.Label.NoZoneMatchesFilter",
                                                    "No zone matches filter",
                                                )}
                                            </span>
                                        )}
                                        {zones[index].map((zoneData, idx) => (
                                            <VC.DropdownItem
                                                key={idx}
                                                className={styles.dropdownItem}
                                                focusKey={VF.FOCUS_DISABLED}
                                                value={zoneData.index}
                                                closeOnSelect={true}
                                                sounds={{ select: "select-item" }}
                                                onChange={(value) => {
                                                    GAME_BINDINGS.ZONE.set(value);
                                                }}>
                                                <div className={styles.dropdownItem__inner}>
                                                    <Icon
                                                        className={c(
                                                            styles.dropdownZoneIcon,
                                                            styles.dropdownIcon,
                                                        )}
                                                        src={zoneData.thumbnail}
                                                    />
                                                    <div className={styles.zoneName}>
                                                        {translate(
                                                            `Assets.NAME[${zoneData.name}]`,
                                                            zoneData.name,
                                                        )}
                                                    </div>
                                                </div>
                                            </VC.DropdownItem>
                                        ))}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </VC.Scrollable>
            </div>
        </div>
    );

    return (
        <VC.Section
            focusKey={VF.FOCUS_DISABLED}
            title={translate("PlatterMod.UI.SectionTitle.Prezoning")}>
            <Dropdown
                focusKey={VF.FOCUS_DISABLED}
                initialFocused={"Test"}
                alignment="left"
                theme={{
                    ...VT.dropdown,
                    dropdownMenu: styles.dropdownMenu,
                }}
                content={DropdownContent}>
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
