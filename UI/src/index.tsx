import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import { trigger } from "cs2/api";
import mod from "mod.json";
import { ToolButton } from "button/button";
import { ToolPanel } from "panel/panel";
import { SelectedInfoPanelComponent } from "infoview/infoview";
import { VanillaComponentResolver } from "utils/VanillaComponentResolver";

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    VanillaComponentResolver.setRegistry(moduleRegistry);

    moduleRegistry.extend(
        "game-ui/game/components/toolbar/top/toolbar-button-strip/toolbar-button-strip.tsx",
        "ToolbarButtonStrip",
        ToolButton
    );
    moduleRegistry.extend(
        "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx",
        'selectedInfoSectionComponents',
        SelectedInfoPanelComponent
    );
    moduleRegistry.append("Game", ToolPanel);

    let results = [];

    for (let i = 2; i <= 6; i++) {
        for (let j = 2; j <= 6; j++) {
            const name = `{ "Assets.NAME[Parcel ${i}x${j}]", "Parcel ${i}x${j}" },`;
            const description = `{ "Assets.DESCRIPTION[Parcel ${i}x${j}]", "Parcel ${i}x${j}" },`;
            results.push(name);
            results.push(description);
        }
    }

    console.log(results.join('\n'));
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
