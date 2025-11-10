import React from "react";
import { Button, Panel, PanelSection, Tooltip } from "cs2/ui";
import styles from "./toolButton.module.scss";
import { VF, VC, VT } from "../vanilla/Components";
import { useValue } from "cs2/api";
import { GAME_BINDINGS } from "gameBindings";
import { c } from "utils/classes";
import { useLocalization } from "cs2/l10n";

export const buttonId = "platterToolButton";
const iconSrc = "coui://platter/logo.svg";

export const ToolButton = () => {
    const [enabled, setIsEnabled] = React.useState(false);
    const { translate } = useLocalization();

    return (
        <>
            {enabled ? <ToolPanel /> : null}
            <Tooltip tooltip={translate("Options.SECTION[Platter.Platter.PlatterMod]")}>
                <Button
                    variant="floating"
                    className={styles.toolButton}
                    onSelect={() => setIsEnabled(!enabled)}
                    src={iconSrc}
                    tooltipLabel={translate("Options.SECTION[Platter.Platter.PlatterMod]")}
                />
            </Tooltip>
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
                    <span className={styles.headerText}>
                        {translate("Options.SECTION[Platter.Platter.PlatterMod]")}
                    </span>
                </div>
            }>
            <PanelSection className={styles.section}>
                <VC.Section title={translate("PlatterMod.UI.SectionTitle.RenderParcels")}>
                    <VC.Checkbox
                        onChange={() => GAME_BINDINGS.RENDER_PARCELS.set(!renderParcelBinding)}
                        focusKey={VF.FOCUS_DISABLED}
                        tooltip={translate("PlatterMod.UI.Tooltip.ToggleRenderParcels")}
                        checked={renderParcelBinding}
                        className={c(VT.checkbox.label)}
                    />
                </VC.Section>
                <VC.Section title={translate("PlatterMod.UI.SectionTitle.AllowSpawn")}>
                    <VC.Checkbox
                        onChange={() => GAME_BINDINGS.ALLOW_SPAWNING.set(!allowSpawningBinding)}
                        focusKey={VF.FOCUS_DISABLED}
                        tooltip={translate("PlatterMod.UI.Tooltip.ToggleAllowSpawn")}
                        checked={allowSpawningBinding}
                        className={c(VT.checkbox.label)}
                    />
                </VC.Section>
            </PanelSection>
        </Panel>
    );
};
