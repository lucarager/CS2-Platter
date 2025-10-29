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
                                <video
                                    src="coui://platter/v1.webm"
                                    autoPlay={true}
                                    loop={true}
                                    className={styles.card__image}></video>
                                <div className={styles.card__text_container}>
                                    {/* eslint-disable-next-line react/no-unknown-property */}
                                    <p className={styles.card__text} cohinline="true">
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
                                    {/* eslint-disable-next-line react/no-unknown-property */}
                                    <p className={styles.card__text} cohinline="true">
                                        You can use the tools familiar to you to zone parcels and
                                        grow buildings.{" "}
                                        <span>
                                            Use the Flood Fill zoning tool for best results,
                                        </span>{" "}
                                        as it will limit the flood area to a parcel.
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
                                    {/* eslint-disable-next-line react/no-unknown-property */}
                                    <p className={styles.card__text} cohinline="true">
                                        {/* eslint-disable-next-line prettier/prettier */}
                                        The <span>top left Platter menu</span> allows you to toggle
                                        the parcel overlay and temporarily block buildings growing
                                        on parcels.
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
                                    {/* eslint-disable-next-line react/no-unknown-property */}
                                    <p className={styles.card__text} cohinline="true">
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
