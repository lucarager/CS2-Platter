import { FocusKey, Theme, UniqueFocusKey } from "cs2/bindings";
import { ModuleRegistry } from "cs2/modding";
import { DropdownItemProps, DropdownProps, DropdownToggleProps } from "cs2/ui";
import { HTMLAttributes } from "react";

// These are specific to the types of components that this mod uses.
// In the UI developer tools at http://localhost:9444/ go to Sources -> Index.js. Pretty print if it is formatted in a single line.
// Search for the tsx or scss files. Look at the function referenced and then find the properties for the component you're interested in.
// As far as I know the types of properties are just guessed.
type PropsToolButton = {
    focusKey?: UniqueFocusKey | null;
    src: string;
    selected?: boolean;
    multiSelect?: boolean;
    disabled?: boolean;
    tooltip?: string | JSX.Element | null;
    selectSound?: any;
    uiTag?: string;
    className?: string;
    children?: string | JSX.Element | JSX.Element[];
    onSelect?: (x: any) => any;
} & HTMLAttributes<any>;

type PropsStepToolButton = {
    focusKey?: UniqueFocusKey | null;
    selectedValue: number;
    values: number[];
    tooltip?: string | null;
    uiTag?: string;
    onSelect?: (x: any) => any;
} & HTMLAttributes<any>;

type TabBarProps = {};

type PropsSection = {
    title?: string | null;
    uiTag?: string;
    children: string | JSX.Element | JSX.Element[];
};

// This is an array of the different components and sass themes that are appropriate for your UI. You need to figure out which ones you need from the registry.
const registryIndex = {
    Section: [
        "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx",
        "Section",
    ],
    ToolButton: [
        "game-ui/game/components/tool-options/tool-button/tool-button.tsx",
        "ToolButton",
    ],
    StepToolButton: [
        "game-ui/game/components/tool-options/tool-button/tool-button.tsx",
        "StepToolButton",
    ],
    TabBar: [
        "game-ui/game/components/asset-menu/asset-category-tab-bar/asset-category-tab-bar.tsx",
        "TabBar",
    ],
    tabBarTheme: [
        "game-ui/game/components/asset-menu/asset-category-tab-bar/asset-category-tab-bar.scss",
        "classes",
    ],
    toolButtonTheme: [
        "game-ui/game/components/tool-options/tool-button/tool-button.module.scss",
        "classes",
    ],
    mouseToolOptionsTheme: [
        "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.module.scss",
        "classes",
    ],
    FOCUS_DISABLED: ["game-ui/common/focus/focus-key.ts", "FOCUS_DISABLED"],
    FOCUS_AUTO: ["game-ui/common/focus/focus-key.ts", "FOCUS_AUTO"],
    useUniqueFocusKey: [
        "game-ui/common/focus/focus-key.ts",
        "useUniqueFocusKey",
    ],
    assetGridTheme: [
        "game-ui/game/components/asset-menu/asset-grid/asset-grid.module.scss",
        "classes",
    ],
    // Dropdown: ["game-ui/common/input/dropdown/dropdown.tsx", "Dropdown"],
    // DropdownItem: ["game-ui/common/input/dropdown/items/dropdown-item.tsx", "DropdownItem"],
    // DropdownToggle: ["game-ui/common/input/dropdown/dropdown-toggle.tsx", "DropdownToggle"],
};

export class VanillaComponentResolver {
    // As far as I know you should not need to edit this portion here.
    // This was written by Klyte for his mod's UI but I didn't have to make any edits to it at all.
    public static get instance(): VanillaComponentResolver {
        return this._instance!;
    }
    private static _instance?: VanillaComponentResolver;

    public static setRegistry(in_registry: ModuleRegistry) {
        this._instance = new VanillaComponentResolver(in_registry);
    }
    private registryData: ModuleRegistry;

    constructor(in_registry: ModuleRegistry) {
        this.registryData = in_registry;
    }

    private cachedData: Partial<Record<keyof typeof registryIndex, any>> = {};
    private updateCache(entry: keyof typeof registryIndex) {
        const entryData = registryIndex[entry];
        return (this.cachedData[entry] = this.registryData.registry.get(
            entryData[0],
        )![entryData[1]]);
    }

    // This section defines your components and themes in a way that you can access via the singleton in your components.
    // Replace the names, props, and strings as needed for your mod.
    public get Section(): (props: PropsSection) => JSX.Element {
        return this.cachedData["Section"] ?? this.updateCache("Section");
    }
    public get ToolButton(): (props: PropsToolButton) => JSX.Element {
        return this.cachedData["ToolButton"] ?? this.updateCache("ToolButton");
    }
    public get StepToolButton(): (props: PropsStepToolButton) => JSX.Element {
        return (
            this.cachedData["StepToolButton"] ??
            this.updateCache("StepToolButton")
        );
    }
    public get TabBar(): (props: TabBarProps) => JSX.Element {
        return this.cachedData["TabBar"] ?? this.updateCache("TabBar");
    }

    public get toolButtonTheme(): Theme | any {
        return (
            this.cachedData["toolButtonTheme"] ??
            this.updateCache("toolButtonTheme")
        );
    }
    public get mouseToolOptionsTheme(): Theme | any {
        return (
            this.cachedData["mouseToolOptionsTheme"] ??
            this.updateCache("mouseToolOptionsTheme")
        );
    }
    public get assetGridTheme(): Theme | any {
        return (
            this.cachedData["assetGridTheme"] ??
            this.updateCache("assetGridTheme")
        );
    }

    public get FOCUS_DISABLED(): UniqueFocusKey {
        return (
            this.cachedData["FOCUS_DISABLED"] ??
            this.updateCache("FOCUS_DISABLED")
        );
    }
    public get FOCUS_AUTO(): UniqueFocusKey {
        return this.cachedData["FOCUS_AUTO"] ?? this.updateCache("FOCUS_AUTO");
    }
    public get useUniqueFocusKey(): (
        focusKey: FocusKey,
        debugName: string,
    ) => UniqueFocusKey | null {
        return (
            this.cachedData["useUniqueFocusKey"] ??
            this.updateCache("useUniqueFocusKey")
        );
    }
    // public get Dropdown(): (props: DropdownProps) => JSX.Element { return this.cachedData["Dropdown"] ?? this.updateCache("Dropdown") }
    // public get DropdownItem(): <T>(props: DropdownItemProps<T>) => JSX.Element { return this.cachedData["DropdownItem"] ?? this.updateCache("DropdownItem") }
    // public get DropdownToggle(): (props: DropdownToggleProps) => JSX.Element { return this.cachedData["DropdownToggle"] ?? this.updateCache("DropdownToggle") }
}
