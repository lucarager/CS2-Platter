import React, { useCallback } from "react";
import { Button, Panel } from "cs2/ui";
import styles from "./styles.module.scss";
import { VT } from "components/vanilla/Components";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import { useValue } from "cs2/api";

export type BlockControlProps = Record<string, never>;

export const WelcomeModal = () => {
    const modalBinding = useValue(GAME_BINDINGS.MODAL__FIRST_LAUNCH.binding);

    const handleCloseOnSelect = useCallback(() => {
        GAME_TRIGGERS.MODAL_DISMISS("first_launch");
    }, []);

    // if (modalBinding) {
    //     return <></>;
    // }

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
                                <p>
                                    Platter&apos;s mission: make creating parcels and zoning
                                    awesome. This first beta release includes ploppable parcels and
                                    lots of tools to help plan your dream city!
                                </p>
                            </div>
                        </span>
                        <Button
                            variant="icon"
                            src="Media/Glyphs/Close.svg"
                            onSelect={handleCloseOnSelect}
                        />
                    </div>
                }>
                <div className={styles.content}>
                    <div className={styles.cards}>
                        <div className={styles.card}>
                            <div className={styles.card__inner}>
                                <img
                                    src="coui://platter/placeholder.png"
                                    className={styles.card__image}
                                />
                                <div className={styles.card__text_container}>
                                    <p className={styles.card__text}>
                                        You can find ploppable parcels in{" "}
                                        <span>the new Platter tab</span> in the zone toolbar. Place
                                        your parcels however you&apos;d like - oh, and no need to
                                        block vanilla blocks!
                                    </p>
                                </div>
                            </div>
                        </div>
                        <div className={styles.card}>
                            <div className={styles.card__inner}>
                                <img
                                    src="coui://platter/placeholder.png"
                                    className={styles.card__image}
                                />
                                <div className={styles.card__text_container}>
                                    <p className={styles.card__text}>
                                        Parcels use the vanilla zoning and building systems - so use
                                        the tools familiar to you to zone them!
                                        <span>
                                            Use the Flood Fill zoning tool for best results
                                        </span>{" "}
                                        - it will limit the flood area to a parcel.
                                    </p>
                                </div>
                            </div>
                        </div>
                        <div className={styles.card}>
                            <div className={styles.card__inner}>
                                <img
                                    src="coui://platter/placeholder.png"
                                    className={styles.card__image}
                                />
                                <div className={styles.card__text_container}>
                                    <p className={styles.card__text}>
                                        The <span>top left Platter menu</span> allows you to toggle
                                        Parcel Rendering or temporarily block anything from growing
                                        on parcels. Don&apos;t forget to check out the{" "}
                                        <span>pre-zoning and snap tools</span> while plopping, too!
                                    </p>
                                </div>
                            </div>
                        </div>
                        <div className={styles.card}>
                            <div className={styles.card__inner}>
                                <img
                                    src="coui://platter/placeholder.png"
                                    className={styles.card__image}
                                />
                                <div className={styles.card__text_container}>
                                    <p className={styles.card__text}>
                                        Lastly, Platter works great with Advanced Line Tool and
                                        MoveIt! Check them out, and I hope you have fun with
                                        Platter!
                                    </p>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </Panel>
        </div>
    );
};
