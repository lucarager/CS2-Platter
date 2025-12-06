import React, { useEffect, useState, useRef } from "react";
import { ModuleRegistryExtend } from "cs2/modding";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import styles from "./toolOptionsPanel.module.scss";
import { Icon, DropdownItem, Tooltip, DropdownToggle, Dropdown } from "cs2/ui";
import { c } from "utils/classes";
import { VF } from "../vanilla/Components";
import { useLocalization } from "cs2/l10n";
import { SnapMode, ZoneData } from "types";
import { useRenderTracker, useValueWrap } from "../../debug";
import { FocusDisabled } from "cs2/input";

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

        return (
            <>
                {enabledBinding && <ToolPanel />}
                <Component {...props} />
            </>
        );
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
        <FocusDisabled>
            <div className={styles.wrapper}>
                <div className={c(VT.toolOptionsPanel.toolOptionsPanel, styles.moddedSection)}>
                    <PrezoningSection />
                    {snapModeBinding != SnapMode.None && <SnapRoadsideSection />}
                    <ParcelWidthSection />
                    <ParcelDepthSection />
                    <SnapModeSection />
                    <ToolViewmodeSection />
                </div>
            </div>
        </FocusDisabled>
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

        // <VC.Section
        //     focusKey={VF.FOCUS_DISABLED}
        //     title={translate("PlatterMod.UI.SectionTitle.Prezoning")}>
        //     <VC.ToolButton
        //         src={activeZone?.thumbnail || "Media/Editor/Thumbnails/Fallback_Generic.svg"}
        //         onSelect={() => setDropdownOpen(!dropdownOpen)}
        //         multiSelect={false}
        //         className={c(VT.toolButton.button, styles.dropdownToggle)}
        //         focusKey={VF.FOCUS_DISABLED}>
        //         <div className={styles.dropdownToggleInner}>
        //             <div className={styles.dropdownToggleLabel}>
        //                 {translate(`Assets.NAME[${activeZone?.name}]`)}
        //             </div>
        //             <Icon
        //                 className={c(styles.dropdownToggleIcon, styles.dropdownIcon)}
        //                 src={"Media/Glyphs/ThickStrokeArrowDown.svg"}
        //             />
        //         </div>
        //     </VC.ToolButton>
        // </VC.Section>
    );
};

const SnapModeSection = function SnapModeSection() {
    useRenderTracker("ToolPanel/SnapModeSection");
    const snapModeBinding = useValueWrap(GAME_BINDINGS.SNAP_MODE.binding, "SnapMode");
    const { translate } = useLocalization();

    return (
        <VC.Section
            focusKey={VF.FOCUS_DISABLED}
            title={translate("PlatterMod.UI.SectionTitle.SnapMode")}>
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
    const snapSpacingMaxBinding = useValueWrap(
        GAME_BINDINGS.MAX_SNAP_SPACING.binding,
        "SnapSpacingMax",
    );
    const { translate } = useLocalization();

    return (
        <VC.Section
            focusKey={VF.FOCUS_DISABLED}
            title={translate("PlatterMod.UI.SectionTitle.SnapSpacing")}>
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
                disabled={snapSpacingBinding === snapSpacingMaxBinding}
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
        <VC.Section
            focusKey={VF.FOCUS_DISABLED}
            title={translate("PlatterMod.UI.SectionTitle.ParcelWidth")}>
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
        <VC.Section
            focusKey={VF.FOCUS_DISABLED}
            title={translate("PlatterMod.UI.SectionTitle.ParcelDepth")}>
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
