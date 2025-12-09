import React, { useEffect, useState } from "react";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS } from "gameBindings";
import styles from "./toolOptionsPanel.module.scss";
import { Icon, DropdownItem, DropdownToggle, Dropdown } from "cs2/ui";
import { c } from "utils/classes";
import { VF } from "../vanilla/Components";
import { useLocalization } from "cs2/l10n";
import { ZoneData } from "types";
import { useRenderTracker, useValueWrap } from "../../debug";

export const PrezoningSection = function PrezoningSection() {
    useRenderTracker("ToolPanel/PrezoningSection");
    const zoneBinding = useValueWrap(GAME_BINDINGS.ZONE.binding, "Zone");
    const zoneDataBinding = useValueWrap(GAME_BINDINGS.ZONE_DATA.binding, "ZoneData");
    const assetPackBinding = useValueWrap(GAME_BINDINGS.ASSET_PACK_DATA.binding, "AssetPackData");
    const { translate } = useLocalization();
    const [selectedPacks, setSelectedPacks] = useState<any[]>([]);
    const [dropdownOpen, setDropdownOpen] = useState<boolean>(true);
    const [searchQuery, setSearchQuery] = useState<string>("");
    const [activeZone, setActivezone] = useState<ZoneData>();

    useEffect(() => {
        const zone = zoneDataBinding.find((z) => z.index == zoneBinding);
        setActivezone(zone);
    }, [zoneBinding, zoneDataBinding]);

    useEffect(() => {
        const handleKeyDown = (e: KeyboardEvent) => {
            if (e.key === "Escape") {
                setDropdownOpen(false);
            }
        };

        if (dropdownOpen) {
            window.addEventListener("keydown", handleKeyDown);
        }

        return () => {
            window.removeEventListener("keydown", handleKeyDown);
        };
    }, [dropdownOpen]);

    const categories = [
        { asFilter: false, name: "None", icon: "Media/Editor/Thumbnails/Fallback_Generic.svg" },
        { asFilter: true, name: "Residential", icon: "Media/Game/Icons/ZoneResidential.svg" },
        { asFilter: true, name: "Commercial", icon: "Media/Game/Icons/ZoneCommercial.svg" },
        { asFilter: true, name: "Industrial", icon: "Media/Game/Icons/ZoneIndustrial.svg" },
        { asFilter: true, name: "Office", icon: "Media/Game/Icons/ZoneOffice.svg" },
    ];

    const allCategoryNames = categories.map((c) => c.name);
    const filterableCategoryNames = categories.filter((c) => c.asFilter).map((c) => c.name);

    const [selectedCategories, setSelectedCategories] = useState<string[]>(allCategoryNames);

    const isPackSelected = (entity: any) =>
        selectedPacks.some((p) => p.index === entity.index && p.version === entity.version);

    const isAllSelected =
        assetPackBinding.length > 0 && assetPackBinding.every((p) => isPackSelected(p.entity));

    const togglePack = (entity: any) => {
        if (isPackSelected(entity)) {
            setSelectedPacks(
                selectedPacks.filter(
                    (p) => p.index !== entity.index || p.version !== entity.version,
                ),
            );
        } else {
            setSelectedPacks([...selectedPacks, entity]);
        }
    };

    const selectAll = () => {
        if (isAllSelected) {
            setSelectedPacks([]);
        } else {
            setSelectedPacks(assetPackBinding.map((p) => p.entity));
        }
    };

    const isCategorySelected = (name: string) => selectedCategories.includes(name);
    const isAllCategoriesSelected = filterableCategoryNames.every((name) =>
        selectedCategories.includes(name),
    );

    const toggleCategory = (name: string) => {
        if (isCategorySelected(name)) {
            setSelectedCategories(selectedCategories.filter((c) => c !== name));
        } else {
            setSelectedCategories([...selectedCategories, name]);
        }
    };

    const selectAllCategories = () => {
        if (isAllCategoriesSelected) {
            setSelectedCategories([]);
        } else {
            setSelectedCategories(allCategoryNames);
        }
    };

    const zones = categories.map((category) => {
        return zoneDataBinding
            .filter((zoneData) => {
                if (zoneData.category != category.name) return false;

                if (searchQuery) {
                    const localizedName = translate(`Assets.NAME[${zoneData.name}]`, zoneData.name);
                    if (!localizedName?.toLowerCase().includes(searchQuery.toLowerCase()))
                        return false;
                }

                if (zoneData.name === "Unzoned") return true;
                if (selectedPacks.length === 0) return true;
                return zoneData.assetPacks.some((p) => isPackSelected(p));
            })
            .sort((a, b) => a.name.localeCompare(b.name));
    });

    const DropdownContent = (
        <div className={styles.dropdownContent}>
            <div className={styles.dropdownContent__sidebar}>
                <VC.Scrollable>
                    <div className={styles.dropdownContent__sidebar__inner}>
                        <div className={styles.dropdownContent__sidebar__row}>
                            <div className={styles.dropdownContent__sidebar__row__label}>
                                {translate("PlatterMod.UI.Label.Search", "Search")}
                            </div>
                            <VC.TextInput
                                multiline={1}
                                disabled={false}
                                onChange={(e: React.ChangeEvent<HTMLInputElement>) =>
                                    setSearchQuery(e?.target?.value)
                                }
                                type="text"
                                value={searchQuery}
                                className={c(VT.textInput.input, styles.textInput)}
                                focusKey={VF.FOCUS_DISABLED}
                            />
                        </div>
                        <div className={styles.dropdownContent__sidebar__row}>
                            <div className={styles.dropdownContent__sidebar__row__label}>
                                {translate("PlatterMod.UI.SectionTitle.Category", "Category")}
                            </div>
                            <div className={styles.dropdownContent__sidebar__row__buttons}>
                                <VC.ToolButton
                                    className={c(VT.toolButton.button, styles.filterButton)}
                                    src={"Media/Tools/Snap Options/All.svg"}
                                    multiSelect={true}
                                    selected={isAllCategoriesSelected}
                                    onSelect={selectAllCategories}
                                    disabled={false}
                                    focusKey={VF.FOCUS_DISABLED}
                                    tooltip={"All"}
                                />
                                {categories
                                    .filter((c) => c.asFilter)
                                    .map((category, index) => (
                                        <VC.ToolButton
                                            key={index}
                                            className={c(VT.toolButton.button, styles.filterButton)}
                                            // Temporary hardcoded icons for zone categories
                                            src={category.icon}
                                            multiSelect={true}
                                            selected={isCategorySelected(category.name)}
                                            onSelect={() => toggleCategory(category.name)}
                                            disabled={false}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={category.name}
                                        />
                                    ))}
                            </div>
                        </div>
                        <div className={styles.dropdownContent__sidebar__row}>
                            <div className={styles.dropdownContent__sidebar__row__label}>
                                {translate("PlatterMod.UI.SectionTitle.AssetPacks", "Asset Packs")}
                            </div>
                            <div className={styles.dropdownContent__sidebar__row__buttons}>
                                <VC.ToolButton
                                    className={c(VT.toolButton.button, styles.filterButton)}
                                    src={"Media/Tools/Snap Options/All.svg"}
                                    multiSelect={true}
                                    selected={isAllSelected}
                                    onSelect={selectAll}
                                    disabled={false}
                                    focusKey={VF.FOCUS_DISABLED}
                                    tooltip={"All"}
                                />
                                {assetPackBinding.map((pack, index) => (
                                    <VC.ToolButton
                                        key={index}
                                        className={c(VT.toolButton.button, styles.filterButton)}
                                        src={pack.icon}
                                        multiSelect={true}
                                        selected={isPackSelected(pack.entity)}
                                        onSelect={() => togglePack(pack.entity)}
                                        disabled={false}
                                        focusKey={VF.FOCUS_DISABLED}
                                        tooltip={pack.name}
                                    />
                                ))}
                            </div>
                        </div>
                    </div>
                </VC.Scrollable>
            </div>
            <div className={styles.dropdownContent__content}>
                <VC.Scrollable>
                    <div className={styles.dropdownContent__content__inner}>
                        {categories.map((category, index) => {
                            if (!selectedCategories.includes(category.name)) return null;

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
                                            <DropdownItem<number>
                                                key={idx}
                                                className={styles.dropdownItem}
                                                focusKey={VF.FOCUS_DISABLED}
                                                value={zoneData.index}
                                                closeOnSelect={true}
                                                sounds={{ select: "select-item" }}
                                                onChange={(value) => {
                                                    GAME_BINDINGS.ZONE.set(value);
                                                    setDropdownOpen(false);
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
                                            </DropdownItem>
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
