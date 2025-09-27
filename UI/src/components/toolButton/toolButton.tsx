import React from "react";
import { Button, Panel, PanelSection } from "cs2/ui";
import styles from "./toolButton.module.scss";
import { VanillaFocusKey, VanillaComponents, VanillaThemes } from "../vanilla/Components";
import { useValue } from "cs2/api";
import { GAME_BINDINGS } from "gameBindings";
import { c } from "utils/classes";
import { useLocalization } from "cs2/l10n";

export const buttonId = "platterToolButton";

export const ToolButton = () => {
    const iconSrc = "coui://platter/logo.svg";
    const [enabled, setIsEnabled] = React.useState(false);

    return (
        <>
            {enabled ? <ToolPanel /> : null}
            <Button variant="floating" onSelect={() => setIsEnabled(!enabled)}>
                <img src={iconSrc} style={{ width: "100%" }} />
            </Button>
        </>
    );
};

export const ToolPanel = () => {
    const renderParcelBinding = useValue(GAME_BINDINGS.RENDER_PARCELS.binding);
    const allowSpawningBinding = useValue(GAME_BINDINGS.ALLOW_SPAWNING.binding);
    const { translate } = useLocalization();

    return (
        <Panel
            className={styles.panel}
            header={
                <div className={styles.header}>
                    <span className={styles.headerText}>Platter</span>
                </div>
            }>
            <PanelSection className={styles.section}>
                <VanillaComponents.Section
                    title={translate("PlatterMod.UI.SectionTitle.RenderParcels")}>
                    <VanillaComponents.Checkbox
                        onChange={() => GAME_BINDINGS.RENDER_PARCELS.set(!renderParcelBinding)}
                        focusKey={VanillaFocusKey.FOCUS_DISABLED}
                        tooltip="Tooltip"
                        checked={renderParcelBinding}
                        className={c(VanillaThemes.checkbox.label)}
                    />
                </VanillaComponents.Section>
                <VanillaComponents.Section
                    title={translate("PlatterMod.UI.SectionTitle.AllowSpawn")}>
                    <VanillaComponents.Checkbox
                        onChange={() => GAME_BINDINGS.ALLOW_SPAWNING.set(!allowSpawningBinding)}
                        focusKey={VanillaFocusKey.FOCUS_DISABLED}
                        tooltip="Tooltip"
                        checked={allowSpawningBinding}
                        className={c(VanillaThemes.checkbox.label)}
                    />
                </VanillaComponents.Section>
            </PanelSection>
        </Panel>
    );
};
