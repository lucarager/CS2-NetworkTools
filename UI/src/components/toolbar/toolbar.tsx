import React from "react";
import styles from "./toolbar.module.scss";
import { Button } from "cs2/ui";
const iconSrc = "coui://uil/Standard/Road.svg";

import iconsCheck from "assets/icons/check.svg";
import iconsRedo from "assets/icons/redo.svg";
import iconsUndo from "assets/icons/undo.svg";

export const Toolbar = () => {
    return <></>;

    return (
        <div className={styles.wrapper}>
            <div className={styles.panel}>
                <div className={styles.row}>
                    <Button variant="round" className={styles.iconButton}>
                        <img src={iconsRedo} className={styles.icon} />
                    </Button>
                    <Button variant="round" className={styles.iconButton}>
                        <img src={iconsUndo} className={styles.icon} />
                    </Button>
                    <div className={styles.divider}></div>
                    <Button variant="primary" className={styles.applyButton}>
                        Apply
                    </Button>
                    <div className={styles.divider}></div>
                    <Button variant="round" className={styles.iconButton}>
                        <img src={iconsCheck} className={styles.icon} />
                    </Button>
                    <Button variant="round" className={styles.iconButton}>
                        <img src={iconsCheck} className={styles.icon} />
                    </Button>
                </div>
            </div>
        </div>
    );
};
