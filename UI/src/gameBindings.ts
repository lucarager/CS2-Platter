import { trigger } from "cs2/api";
import mod from "../mod.json";
import { TwoWayBinding } from "utils/bidirectionalBinding";
import { ParcelUIData, PrefabData, SnapMode, ZoneData } from "types";
import { Entity } from "cs2/bindings";

export const GAME_BINDINGS = {
    TOOL_ENABLED: new TwoWayBinding<boolean>("TOOL_ENABLED", false),
    ALLOW_SPAWNING_INFO_SECTION: new TwoWayBinding<boolean>("ALLOW_SPAWNING_INFO_SECTION", false),
    BLOCK_WIDTH: new TwoWayBinding<number>("BLOCK_WIDTH", 2),
    BLOCK_DEPTH: new TwoWayBinding<number>("BLOCK_DEPTH", 2),
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
    ADJUST_BLOCK_SIZE: (action: string) => {
        trigger(mod.id, "TRIGGER:ADJUST_BLOCK_SIZE", action);
    },
    MODAL_DISMISS: (modal: string) => {
        trigger(mod.id, "TRIGGER:MODAL_DISMISS", modal);
    },
    INFOPANEL_SELECT_PARCEL_ENTITY: (entity: Entity) => {
        trigger(mod.id, "TRIGGER:INFOPANEL_SELECT_PARCEL_ENTITY", entity);
    },
    INFOPANEL_PARCEL_RELOCATE: (entity: Entity) => {
        trigger(mod.id, "TRIGGER:INFOPANEL_PARCEL_RELOCATE", entity);
    },
    ROAD_SIDE__REQUEST_APPLY: () => {
        trigger(mod.id, "ROAD_SIDE__REQUEST_APPLY");
    }
};
