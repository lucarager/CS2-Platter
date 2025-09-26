import React from "react";
import { UniqueFocusKey } from "cs2/bindings";
import { HTMLAttributes } from "react";

export interface IVanillaComponents {
    Section: React.FC<VanillaSectionProps>;
    StepToolButton: React.FC<VanillaStepToolButtonProps>;
    TabBar: React.FC<VanillaTabBarProps>;
    Checkbox: React.FC<VanillaCheckboxProps>;
    ToolButton: React.FC<VanillaToolButtonProps>;
    [key: string]: React.FC<any>;
}

export interface IVanillaThemes {
    toolButtonTheme: Record<"button", string>;
    mouseToolOptionsTheme: Record<"startButton" | "numberField" | "endButton", string>;
    dropdownTheme: Record<"dropdownItem" | "dropdownToggle", string>;
    checkboxTheme: Record<"label", string>;
    toolbarFeatureButton: Record<"toolbarFeatureButton" | "button", string>;
    [key: string]: Record<string, string>;
}

export interface IVanillaFocus {
    FOCUS_DISABLED: UniqueFocusKey;
    FOCUS_AUTO: UniqueFocusKey;
}

export type VanillaToolButtonProps = {
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

export type VanillaStepToolButtonProps = {
    focusKey?: UniqueFocusKey | null;
    selectedValue: number;
    values: number[];
    tooltip?: string | null;
    uiTag?: string;
    onSelect?: (x: any) => any;
} & HTMLAttributes<any>;

export type VanillaTabBarProps = any;

export type VanillaSectionProps = {
    title?: string | null;
    uiTag?: string;
    children: string | JSX.Element | JSX.Element[];
    focusKey?: UniqueFocusKey | null;
};

export type VanillaCheckboxProps = {
    checked?: boolean;
    disabled?: boolean;
    theme?: any;
    className?: string;
    [key: string]: any;
};
