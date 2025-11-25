import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import { GAME_BINDINGS } from "gameBindings";
import { PlatterToolOptionsPanel } from "components/toolOptionsPanel/toolOptionsPanel";
import { initialize } from "components/vanilla/Components";
import { ParcelInfoPanelComponent } from "components/infoview/parcel";
import { BuildingInfoPanelComponent } from "components/infoview/building";
import { ToolButton } from "components/toolButton/toolButton";
import { WelcomeModal } from "components/modals/WelcomeModal";
import { PlatterCreateParcelFromZone } from "components/createParcelFromZone/createParcelFromZone";
import mod from "../mod.json";

// Register bindings
GAME_BINDINGS.BLOCK_DEPTH;

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    initialize(moduleRegistry);

    moduleRegistry.append("GameTopLeft", ToolButton);
    moduleRegistry.append("Game", WelcomeModal);

    moduleRegistry.extend(
        "game-ui/game/components/tool-options/tool-options-panel.tsx",
        "ToolOptionsPanel",
        PlatterToolOptionsPanel,
    );

    moduleRegistry.extend(
        "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx",
        "MouseToolOptions",
        PlatterCreateParcelFromZone,
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

    console.log(mod.id + " UI module registrations completed.");
};

export default register;
