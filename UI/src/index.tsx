import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import { ToolButton } from "toolButton/button";
import { ToolPanel } from "panel/panel";
import { SelectedInfoPanelComponent } from "infoview/infoview";
import { VanillaComponentResolver } from "utils/VanillaComponentResolver";
import { $bindings } from "modBindings";
import mod from "mod.json";

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    VanillaComponentResolver.setRegistry(moduleRegistry);

    // Register bindings
    $bindings.blockDepth;

    moduleRegistry.extend(
        "game-ui/game/components/toolbar/top/toolbar-button-strip/toolbar-button-strip.tsx",
        "ToolbarButtonStrip",
        ToolButton,
    );
    moduleRegistry.extend(
        "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx",
        "selectedInfoSectionComponents",
        SelectedInfoPanelComponent,
    );

    moduleRegistry.append("Game", ToolPanel);

    // This is just to verify using UI console that all the component registriations was completed.
    console.log(mod.id + " UI module registrations completed.");
};

export default register;
