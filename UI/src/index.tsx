import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import { GAME_BINDINGS } from "gameBindings";
import { PlatterMouseToolOptions } from "components/mouseToolOptions/mouseToolOptions";
import { initialize } from "components/vanilla/Components";
import { ParcelInfoPanelComponent } from "components/infoview/parcel";
import { BuildingInfoPanelComponent } from "components/infoview/building";
import { ToolButton } from "components/toolButton/toolButton";
import { WelcomeModal } from "components/modals/WelcomeModal";

// Register bindings
GAME_BINDINGS.BLOCK_DEPTH;

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    initialize(moduleRegistry);

    moduleRegistry.append("GameTopLeft", ToolButton);
    moduleRegistry.append("Game", WelcomeModal);

    moduleRegistry.extend(
        "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx",
        "MouseToolOptions",
        PlatterMouseToolOptions,
    );

    moduleRegistry.extend(
        "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx",
        "selectedInfoSectionComponents",
        ParcelInfoPanelComponent,
    );

    moduleRegistry.extend(
        "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx",
        "selectedInfoSectionComponents",
        BuildingInfoPanelComponent,
    );

    // moduleRegistry.extend(
    //     "game-ui/game/components/game-main-screen.tsx",
    //     "GameMainScreen",
    //     WelcomeModal,
    // );
};

export default register;
