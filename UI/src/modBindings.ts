import { trigger } from "cs2/api";
import mod from "../mod.json";
import { BidirectionalBinding as TwoWayBinding } from "utils/bidirectionalBinding";
import { PrefabData, ZoneData } from "types";

export const GAME_BINDINGS = {
    TOOL_ENABLED: new TwoWayBinding<boolean>("TOOL_ENABLED", false),
    TOOL_MODE: new TwoWayBinding<number>("TOOL_MODE", 0),
    ALLOW_SPAWNING_INFO_SECTION: new TwoWayBinding<boolean>("ALLOW_SPAWNING_INFO_SECTION", false),
    POINTS_COUNT: new TwoWayBinding<number>("POINTS_COUNT", 0),
    BLOCK_WIDTH: new TwoWayBinding<number>("BLOCK_WIDTH", 2),
    BLOCK_DEPTH: new TwoWayBinding<number>("BLOCK_DEPTH", 2),
    ZONE: new TwoWayBinding<number>("ZONE", 0),
    RE_SPACING: new TwoWayBinding<number>("RE_SPACING", 1),
    RE_OFFSET: new TwoWayBinding<number>("RE_OFFSET", 2),
    RE_SIDES: new TwoWayBinding<boolean[]>("RE_SIDES", [true, true, false, false]),
    PREFAB_DATA: new TwoWayBinding<PrefabData>("PREFAB_DATA", {
        name: undefined,
        thumbnail: undefined,
    }),
    ZONE_DATA: new TwoWayBinding<ZoneData[]>("ZONE_DATA", []),
    RENDER_PARCELS: new TwoWayBinding<boolean>("RENDER_PARCELS", true),
    ENABLE_TOOL_BUTTONS: new TwoWayBinding<boolean>("ENABLE_TOOL_BUTTONS", true),
};

export const GAME_TRIGGERS = {
    ADJUST_BLOCK_SIZE: (action: string) => {
        console.log("ADJUST_BLOCK_SIZE", action);
        trigger(mod.id, "TRIGGER:ADJUST_BLOCK_SIZE", action);
    },
    REQUEST_APPLY: () => {
        trigger(mod.id, "TRIGGER:REQUEST_APPLY");
    },
};
