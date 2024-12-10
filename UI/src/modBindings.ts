import { bindValue } from "cs2/api";
import { trigger, useValue } from "cs2/api";
import mod from "../mod.json";

export const bindings = {
    toolEnabled: bindValue<boolean>(mod.id, 'BINDING:TOOL_ENABLED', false),
    infoSectionAllowSpawningToggle: bindValue<boolean>(mod.id, 'BINDING:ALLOW_SPAWNING_INFO_SECTION', false),
    blockWidth: bindValue<number>(mod.id, 'BINDING:BLOCK_WIDTH', 2),
    blockDepth: bindValue<number>(mod.id, 'BINDING:BLOCK_DEPTH', 2),
    prefab: bindValue<string | null>(mod.id, 'BINDING:PREFAB', null),
};

export const events = {
    toggleToolEvent: "EVENT:TOGGLE_TOOL",
}

export const triggers = {
    buttonPress: (action: string) => {
        trigger(mod.id, "EVENT:BUTTON_PRESS", action);
    },
    allowSpawningToggle: () => {
        trigger(mod.id, "EVENT:ALLOW_SPAWNING_TOGGLED");
    },
    toggleTool: () => {
        trigger(mod.id, "EVENT:TOGGLE_TOOL");
    }
}
