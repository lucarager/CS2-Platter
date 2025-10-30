/* eslint-disable prettier/prettier */
/* eslint-disable react/no-unknown-property */
import React, { useCallback, useState } from "react";
import { Button, Panel } from "cs2/ui";
import styles from "./styles.module.scss";
import { VC, VT } from "components/vanilla/Components";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import { useValue } from "cs2/api";

export type BlockControlProps = Record<string, never>;

export const WelcomeModal = () => {
    const modalBinding = useValue(GAME_BINDINGS.MODAL__FIRST_LAUNCH.binding);
    const [activePage, setActivePage] = useState(0);

    const handleCloseOnSelect = useCallback(() => {
        GAME_TRIGGERS.MODAL_DISMISS("first_launch");
    }, []);

    if (modalBinding) {
        return <></>;
    }

    return (
        <div className={styles.wrapper}>
            <Panel
                className={styles.panel}
                theme={{
                    ...VT.panel,
                    header: styles.header,
                }}
                header={
                    <div className={styles.header__inner}>
                        <span className={styles.headerText}>
                            <div className={styles.intro}>
                                <h2>Thanks for installing Platter!</h2>
                                <h3>Here&apos;s a quick intro to get you started</h3>
                            </div>
                        </span>
                        <Button
                            variant="icon"
                            src="Media/Glyphs/Close.svg"
                            onSelect={handleCloseOnSelect}
                        />
                    </div>
                }>
                <VC.PageSwitcher
                    className={styles.pageSwitcher}
                    transitionStyles={VT.horizontalTransition}
                    activePage={activePage}>
                    {activePage != 0 ? null : (
                        <VC.Page className={VT.whatsNewPage["whats-new-page"]} key={0}>
                            <VC.Scrollable>
                                <div className={styles.card}>
                                    <div className={styles.card__inner}>
                                        <div className={styles.card__inner__image}>
                                            <img
                                                src="coui://platter/tu1.png"
                                                className={styles.card__image}
                                            />
                                        </div>
                                        <div className={styles.card__text_container}>
                                            <h3>Platter adds &quot;Parcels&quot; to the game.</h3>
                                            <p className={styles.card__text} cohinline="true">
                                                You can find ploppable parcels in{" "}
                                                <span>the new Platter tab</span>{" "}in the zone
                                                toolbar.
                                            </p>
                                            <p className={styles.card__text} cohinline="true">
                                                Oh, and no need to block or remove vanilla blocks,
                                                plop parcels right on top!
                                            </p>
                                        </div>
                                    </div>
                                </div>
                            </VC.Scrollable>
                        </VC.Page>
                    )}
                    {activePage != 1 ? null : (
                        <VC.Page className={VT.whatsNewPage["whats-new-page"]} key={1}>
                            <VC.Scrollable>
                                <div className={styles.card}>
                                    <div className={styles.card__inner}>
                                        <div className={styles.card__inner__image}>
                                            <img
                                                src="coui://platter/tu2.png"
                                                className={styles.card__image}
                                            />
                                        </div>
                                        <div className={styles.card__text_container}>
                                            <h3>Parcels work just like vanilla blocks.</h3>
                                            <p className={styles.card__text} cohinline="true">
                                                You can use the tools familiar to you to zone and
                                                grow buildings.{" "}
                                                <span>Use the Fill zone tool for best results</span>{" "}
                                                - it will limit the flood area to a parcel.
                                            </p>
                                        </div>
                                    </div>
                                </div>
                            </VC.Scrollable>
                        </VC.Page>
                    )}
                    {activePage != 2 ? null : (
                        <VC.Page className={VT.whatsNewPage["whats-new-page"]} key={2}>
                            <VC.Scrollable>
                                <div className={styles.card}>
                                    <div className={styles.card__inner}>
                                        <div className={styles.card__inner__image}>
                                            <img
                                                src="coui://platter/tu3.png"
                                                className={styles.card__image}
                                            />
                                        </div>
                                        <div className={styles.card__text_container}>
                                            <p className={styles.card__text} cohinline="true">
                                                The <span>top left Platter menu</span>{" "}allows you to
                                                toggle the parcel overlay and temporarily block
                                                buildings growing on parcels.
                                            </p>
                                        </div>
                                    </div>
                                    <div className={styles.card__inner}>
                                        <div className={styles.card__inner__image}>
                                            <img
                                                src="coui://platter/tu4.png"
                                                className={styles.card__image}
                                            />
                                        </div>
                                        <div className={styles.card__text_container}>
                                            <p className={styles.card__text} cohinline="true">
                                                <span>Advanced Line Tool and MoveIt</span>{" "}are great
                                                mods to use with parcels!
                                            </p>
                                        </div>
                                    </div>
                                </div>
                            </VC.Scrollable>
                        </VC.Page>
                    )}
                    {activePage != 3 ? null : (
                        <VC.Page className={VT.whatsNewPage["whats-new-page"]} key={3}>
                            <VC.Scrollable>
                                <div className={styles.card}>
                                    <div className={styles.card__inner}>
                                        {/* <img
                                        src="coui://platter/placeholder.png"
                                        className={styles.card__image}
                                    /> */}
                                        <div className={styles.card__text_container}>
                                            <h3>Disclaimer</h3>
                                            <p className={styles.alert}>
                                                Platter is an experimental beta mod. Should you wish
                                                to uninstall it, the Settings page contains an
                                                uninstall button that will safely remove all custom
                                                parcels from your save.
                                            </p>
                                            <div className={styles.action}>
                                                <Button
                                                    variant="primary"
                                                    onSelect={handleCloseOnSelect}>
                                                    Get plattin&apos;!
                                                </Button>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </VC.Scrollable>
                        </VC.Page>
                    )}
                </VC.PageSwitcher>
                <VC.PageSelector
                    pages={4}
                    selected={activePage}
                    onSelect={(i) => {
                        setActivePage(i);
                    }}
                />
            </Panel>
        </div>
    );
};
