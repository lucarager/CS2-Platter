import React, { memo, useEffect, useState } from "react";
import { Button, Panel, PanelSection, Tooltip } from "cs2/ui";
import styles from "./toolButton.module.scss";
import { VF, VC, VT } from "../vanilla/Components";
import { GAME_BINDINGS } from "gameBindings";
import { c } from "utils/classes";
import { useLocalization } from "cs2/l10n";
import { useRenderTracker, useValueWrap } from "../../debug";

export const buttonId = "platterToolButton";
const iconSrc = "coui://platter/logo.svg";

export const ToolButtonWrapper = () => {
    return <ToolButton />;
};

export const ToolButton = memo(function ToolButton() {
    useRenderTracker("ToolButton");
    const [enabled, setIsEnabled] = React.useState(false);
    const { translate } = useLocalization();

    const currentChangelogVersion = useValueWrap(
        GAME_BINDINGS.CURRENT_CHANGELOG_VERSION.binding,
        "CurrentChangelogVersion",
    );
    const lastViewedChangelogVersion = useValueWrap(
        GAME_BINDINGS.LAST_VIEWED_CHANGELOG_VERSION.binding,
        "LastViewedChangelogVersion",
    );

    const showUpdateBadge = true; // currentChangelogVersion > lastViewedChangelogVersion;

    // Mark changelog as viewed when panel is opened
    useEffect(() => {
        if (enabled && currentChangelogVersion > lastViewedChangelogVersion) {
            GAME_BINDINGS.LAST_VIEWED_CHANGELOG_VERSION.set(currentChangelogVersion);
        }
    }, [enabled, currentChangelogVersion, lastViewedChangelogVersion]);

    return (
        <>
            {enabled && <ToolPanel initialShowUpdateBadge={showUpdateBadge} />}
            <Tooltip
                tooltip={translate("Options.SECTION[Platter.Platter.PlatterMod]")}
                delayTime={0}>
                <Button
                    variant="floating"
                    className={styles.toolButton}
                    onSelect={() => setIsEnabled(!enabled)}
                    src={iconSrc}
                    focusKey={VF.FOCUS_DISABLED}
                    tooltipLabel={translate("Options.SECTION[Platter.Platter.PlatterMod]")}>
                    {showUpdateBadge && <div className={styles.updateBadge}></div>}
                </Button>
            </Tooltip>
        </>
    );
});

export const ToolPanel = memo(function ToolPanel(props: { initialShowUpdateBadge?: boolean }) {
    const [showUpdateBadge, setShowUpdateBadge] = useState(props.initialShowUpdateBadge);
    useRenderTracker("ToolPanel");
    const renderParcelBinding = useValueWrap(GAME_BINDINGS.RENDER_PARCELS.binding, "RenderParcels");
    const allowSpawningBinding = useValueWrap(
        GAME_BINDINGS.ALLOW_SPAWNING.binding,
        "AllowSpawning",
    );
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
                <VC.Section
                    title={translate("PlatterMod.UI.SectionTitle.Changelog", "View Changelog")}>
                    <div style={{ position: "relative" }}>
                        {showUpdateBadge && <div className={styles.updateBadge}></div>}
                        <VC.ToolButton
                            src={"coui://platter/changelog.svg"}
                            onSelect={() => GAME_BINDINGS.MODAL__CHANGELOG.set(true)}
                            focusKey={VF.FOCUS_DISABLED}
                            tooltip={translate("PlatterMod.UI.Tooltip.Changelog")}
                            className={c(VT.checkbox.label)}
                        />
                    </div>
                </VC.Section>
            </PanelSection>
        </Panel>
    );
});
