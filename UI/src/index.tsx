import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import { GAME_BINDINGS } from "gameBindings";
import { PlatterMouseToolOptions } from "components/mouseToolOptions/mouseToolOptions";
import { initialize } from "components/vanilla/Components";
import { SelectedInfoPanelComponent } from "components/infoview/infoview";
import { ToolButton } from "components/toolButton/toolButton";

// Register bindings
GAME_BINDINGS.BLOCK_DEPTH;

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    initialize(moduleRegistry);

    moduleRegistry.append("GameTopLeft", ToolButton);

    moduleRegistry.extend(
        "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx",
        "MouseToolOptions",
        PlatterMouseToolOptions,
    );

    moduleRegistry.extend(
        "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx",
        "selectedInfoSectionComponents",
        SelectedInfoPanelComponent,
    );
};

export default register;
