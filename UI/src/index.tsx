import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import { trigger } from "cs2/api";
import mod from "mod.json";
import { ToolButton } from "tool-button";

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    moduleRegistry.extend(
        "game-ui/game/components/toolbar/top/toolbar-button-strip/toolbar-button-strip.tsx", 
        "ToolbarButtonStrip", 
        ToolButton
    );
}

declare global {
    interface Window { Platter: any; }
}


window.Platter = {
    dostuff: (args: any) => {
        trigger(mod.id, "dostuff", args);
    }
};

export default register;