import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import { ToolButton } from "components/toolButton/toolButton";
import { ToolPanel } from "components/toolPanel/toolPanel";
import { SelectedInfoPanelComponent } from "components/infoview/infoview";
import { VanillaComponentResolver } from "utils/VanillaComponentResolver";
import { $bindings } from "modBindings";
import mod from "mod.json";

// Register bindings
$bindings.blockDepth;

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    VanillaComponentResolver.setRegistry(moduleRegistry);

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

    console.log(mod.id + " UI module registrations completed.");
};

export default register;
