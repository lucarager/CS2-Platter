import { ModRegistrar } from "cs2/modding";
import { HelloWorldComponent } from "mods/hello-world";
import { trigger } from "cs2/api";
import mod from "mod.json";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.append('Menu', HelloWorldComponent);
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