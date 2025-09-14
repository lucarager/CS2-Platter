import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import { GAME_BINDINGS } from "modBindings";
import { PlatterMouseToolOptionsExtension } from "components/mouseToolOptions/mouseToolOptions";
import { initialize } from "components/vanilla/Components";
import { SelectedInfoPanelComponent } from "components/infoview/infoview";
import { ToolPanel } from "components/toolPanel/toolPanel";

// Register bindings
GAME_BINDINGS.BLOCK_DEPTH;

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    initialize(moduleRegistry);

    moduleRegistry.extend(
        "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx",
        "MouseToolOptions",
        PlatterMouseToolOptionsExtension,
    );

    // moduleRegistry.extend(
    //     "game-ui/game/components/toolbar/top/toolbar-button-strip/toolbar-button-strip.tsx",
    //     "ToolbarButtonStrip",
    //     ToolButton,
    // );

    moduleRegistry.extend(
        "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx",
        "selectedInfoSectionComponents",
        SelectedInfoPanelComponent,
    );

    moduleRegistry.append("Game", ToolPanel);
};

export default register;
