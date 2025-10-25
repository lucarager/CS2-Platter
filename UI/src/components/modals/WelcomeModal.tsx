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
                        <span className={styles.headerText}></span>
                        <Button
                            variant="icon"
                            src="Media/Glyphs/Close.svg"
                            onSelect={handleCloseOnSelect}
                        />
                    </div>
                }>
                <div className={styles.content}>
                    <h2>Thanks for installing Platter!</h2>
                    <p>
                        Lorem ipsum dolor sit amet, consectetur adipisicing elit. Doloribus enim
                        dolor dignissimos perferendis, maxime possimus voluptas aperiam facilis
                        voluptate id aliquid labore illo ducimus distinctio vitae rerum, eius
                        incidunt voluptatum.
                    </p>
                    <div className={styles.row}>
                        <div className={styles.card}>
                            <div className={styles.card__inner}>
                                <img
                                    src="coui://platter/tutorial_1.png"
                                    className={styles.card__image}
                                />
                                <p className={styles.card__text}>
                                    Lorem ipsum dolor sit amet, consectetur adipisicing elit.
                                    Doloribus enim enim dolor dignissimos perferendis
                                </p>
                            </div>
                        </div>
                        <div className={styles.card}>
                            <div className={styles.card__inner}>
                                <img
                                    src="coui://platter/tutorial_2.png"
                                    className={styles.card__image}
                                />
                                <p className={styles.card__text}>
                                    Lorem ipsum dolor sit amet, consectetur adipisicing elit.
                                    Doloribus enim enim dolor dignissimos perferendis
                                </p>
                            </div>
                        </div>
                        <div className={styles.card}>
                            <div className={styles.card__inner}>
                                <img
                                    src="coui://platter/tutorial_3.png"
                                    className={styles.card__image}
                                />
                                <p className={styles.card__text}>
                                    Lorem ipsum dolor sit amet, consectetur adipisicing elit.
                                    Doloribus enim enim dolor dignissimos perferendis
                                </p>
                            </div>
                        </div>
                    </div>
                </div>
            </Panel>
        </div>
    );
};
