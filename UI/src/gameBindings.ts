import { TwoWayBinding } from "utils/bidirectionalBinding";
import { ParcelUIData, PrefabData, SnapMode, ZoneData } from "types";
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
    ZONE_DATA: new TwoWayBinding<ZoneData[]>("ZONE_DATA", []),
    RENDER_PARCELS: new TwoWayBinding<boolean>("RENDER_PARCELS", true),
    ALLOW_SPAWNING: new TwoWayBinding<boolean>("ALLOW_SPAWNING", true),
    SNAP_MODE: new TwoWayBinding<SnapMode>("SNAP_MODE", 0),
    SNAP_SPACING: new TwoWayBinding<number>("SNAP_SPACING", 0),

    ENABLE_TOOL_BUTTONS: new TwoWayBinding<boolean>("ENABLE_TOOL_BUTTONS", true),
    ENABLE_CREATE_FROM_ZONE: new TwoWayBinding<boolean>("ENABLE_CREATE_FROM_ZONE", false),

    ZONE: new TwoWayBinding<number>("ZONE", 0),
    TOOL_MODE: new TwoWayBinding<number>("TOOL_MODE"),

    MODAL__FIRST_LAUNCH: new TwoWayBinding<boolean>("MODAL__FIRST_LAUNCH", false),

    INFOPANEL_BUILDING_PARCEL_ENTITY: new TwoWayBinding<Entity>("INFOPANEL_BUILDING_PARCEL_ENTITY"),
    INFOPANEL_PARCEL_DATA: new TwoWayBinding<ParcelUIData>("INFOPANEL_PARCEL_DATA"),
    INFOPANEL_PARCEL_DATA_BUILDING: new TwoWayBinding<Entity>("INFOPANEL_PARCEL_DATA_BUILDING"),
    INFOPANEL_PARCEL_DATA_ROAD: new TwoWayBinding<Entity>("INFOPANEL_PARCEL_DATA_ROAD"),

    ROAD_SIDE__SIDES: new TwoWayBinding<number>("ROAD_SIDE__SIDES"),
    ROAD_SIDE__SPACING: new TwoWayBinding<number>("ROAD_SIDE__SPACING"),
    ROAD_SIDE__OFFSET: new TwoWayBinding<number>("ROAD_SIDE__OFFSET"),
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
