import { TwoWayBinding } from "utils/bidirectionalBinding";
import { AssetPackData, ParcelUIData, PrefabData, SnapMode, ZoneData, ZoneGroupData } from "types";
import { Entity } from "cs2/bindings";
import TriggerBuilder from "utils/trigger";

export const GAME_BINDINGS = {
    TOOL_ENABLED: new TwoWayBinding<boolean>("TOOL_ENABLED", false),
    ALLOW_SPAWNING_INFO_SECTION: new TwoWayBinding<boolean>("ALLOW_SPAWNING_INFO_SECTION", false),
    BLOCK_WIDTH: new TwoWayBinding<number>("BLOCK_WIDTH", 2),
    BLOCK_WIDTH_MIN: new TwoWayBinding<number>("BLOCK_WIDTH_MIN", 1),
    BLOCK_WIDTH_MAX: new TwoWayBinding<number>("BLOCK_WIDTH_MAX", 6),
    BLOCK_DEPTH: new TwoWayBinding<number>("BLOCK_DEPTH", 2),
    BLOCK_DEPTH_MIN: new TwoWayBinding<number>("BLOCK_DEPTH_MIN", 2),
    BLOCK_DEPTH_MAX: new TwoWayBinding<number>("BLOCK_DEPTH_MAX", 6),
    PREFAB_DATA: new TwoWayBinding<PrefabData>("PREFAB_DATA", {
        name: undefined,
        thumbnail: undefined,
    }),
    ASSET_PACK_DATA: new TwoWayBinding<AssetPackData[]>("ASSET_PACK_DATA", []),
    ZONE_DATA: new TwoWayBinding<ZoneData[]>("ZONE_DATA", []),
    ZONE_GROUP_DATA: new TwoWayBinding<ZoneGroupData[]>("ZONE_GROUP_DATA", []),
    RENDER_PARCELS: new TwoWayBinding<boolean>("RENDER_PARCELS", true),
    ALLOW_SPAWNING: new TwoWayBinding<boolean>("ALLOW_SPAWNING", true),
    SNAP_MODE: new TwoWayBinding<SnapMode>("SNAP_MODE", 0),
    SNAP_SPACING: new TwoWayBinding<number>("SNAP_SPACING", 0),
    ENABLE_SNAPPING_OPTIONS: new TwoWayBinding<boolean>("ENABLE_SNAPPING_OPTIONS", false),
    SHOW_ZONES: new TwoWayBinding<boolean>("SHOW_ZONES", false),
    SHOW_CONTOUR_LINES: new TwoWayBinding<boolean>("SHOW_CONTOUR_LINES", false),
    ZONE: new TwoWayBinding<number>("ZONE", 0),
    TOOL_MODE: new TwoWayBinding<number>("TOOL_MODE"),
    MAX_SNAP_SPACING: new TwoWayBinding<number>("MAX_SNAP_SPACING", 8),

    ENABLE_TOOL_BUTTONS: new TwoWayBinding<boolean>("ENABLE_TOOL_BUTTONS", true),
    ENABLE_CREATE_FROM_ZONE: new TwoWayBinding<boolean>("ENABLE_CREATE_FROM_ZONE", false),

    MODAL__FIRST_LAUNCH: new TwoWayBinding<boolean>("MODAL__FIRST_LAUNCH", false),
    MODAL__CHANGELOG: new TwoWayBinding<boolean>("MODAL__CHANGELOG", false),

    CURRENT_CHANGELOG_VERSION: new TwoWayBinding<number>("CURRENT_CHANGELOG_VERSION", 0),
    LAST_VIEWED_CHANGELOG_VERSION: new TwoWayBinding<number>("LAST_VIEWED_CHANGELOG_VERSION", 0),

    INFOPANEL_BUILDING_PARCEL_ENTITY: new TwoWayBinding<Entity>("INFOPANEL_BUILDING_PARCEL_ENTITY"),
    INFOPANEL_PARCEL_DATA: new TwoWayBinding<ParcelUIData>("INFOPANEL_PARCEL_DATA"),
    INFOPANEL_PARCEL_DATA_BUILDING: new TwoWayBinding<Entity>("INFOPANEL_PARCEL_DATA_BUILDING"),
    INFOPANEL_PARCEL_DATA_ROAD: new TwoWayBinding<Entity>("INFOPANEL_PARCEL_DATA_ROAD"),
};

export const GAME_TRIGGERS = {
    ADJUST_BLOCK_SIZE: TriggerBuilder.create<[string]>("ADJUST_BLOCK_SIZE"),
    MODAL_DISMISS: TriggerBuilder.create<[string]>("MODAL_DISMISS"),
    INFOPANEL_SELECT_PARCEL_ENTITY: TriggerBuilder.create<[Entity]>(
        "INFOPANEL_SELECT_PARCEL_ENTITY",
    ),
    INFOPANEL_PARCEL_RELOCATE: TriggerBuilder.create<[Entity]>("INFOPANEL_PARCEL_RELOCATE"),
    ROAD_SIDE__REQUEST_APPLY: TriggerBuilder.create<[]>("ROAD_SIDE__REQUEST_APPLY"),
    CREATE_PARCEL_WITH_ZONE: TriggerBuilder.create<[]>("CREATE_PARCEL_WITH_ZONE"),
};
