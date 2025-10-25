import { ModuleRegistry } from "cs2/modding";
import type { IVanillaComponents, IVanillaFocus, IVanillaThemes as IVanillaThemes } from "./types";

const modulePaths = [
    {
        path: "game-ui/game/components/tool-options/tool-button/tool-button.tsx",
        components: ["ToolButton"],
    },
    {
        path: "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx",
        components: ["Section"],
    },
    {
        path: "game-ui/common/input/toggle/checkbox/checkbox.tsx",
        components: ["Checkbox"],
    },
    {
        path: "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx",
        components: ["InfoSection"],
    },
    {
        path: "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx",
        components: ["InfoRow"],
    },
    {
        path: "game-ui/game/components/selected-info-panel/shared-components/info-link/info-link.tsx",
        components: ["InfoLink"],
    },
];

const themePaths = [
    {
        path: "game-ui/game/components/tool-options/tool-button/tool-button.module.scss",
        name: "toolButton",
    },
    {
        path: "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.module.scss",
        name: "mouseToolOptions",
    },
    {
        path: "game-ui/common/tooltip/description-tooltip/description-tooltip.module.scss",
        name: "descriptionsTooltip",
    },
    {
        path: "game-ui/common/input/dropdown/dropdown.module.scss",
        name: "dropdown",
    },
    {
        path: "game-ui/game/components/statistics-panel/menu/item/statistics-item.module.scss",
        name: "checkbox",
    },
    {
        path: "game-ui/game/components/toolbar/components/feature-button/toolbar-feature-button.module.scss",
        name: "toolbarFeatureButton",
    },
    {
        path: "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss",
        name: "infoRow",
    },
    {
        path: "game-ui/common/panel/themes/default.module.scss",
        name: "panel",
    },
];

export const VC = {} as IVanillaComponents;
export const VT = {} as IVanillaThemes;
export const VF = {} as IVanillaFocus;

export const initialize = (moduleRegistry: ModuleRegistry) => {
    modulePaths.forEach(({ path, components }) => {
        const module = moduleRegistry.registry.get(path);
        components.forEach((component) => (VC[component] = module?.[component]));
    });
    themePaths.forEach(({ path, name }) => {
        const module = moduleRegistry.registry.get(path)?.classes;
        VT[name] = module ?? {};
    });

    const focusKey = moduleRegistry.registry.get("game-ui/common/focus/focus-key.ts");
    VF.FOCUS_DISABLED = focusKey?.FOCUS_DISABLED;
    VF.FOCUS_AUTO = focusKey?.FOCUS_AUTO;
};
