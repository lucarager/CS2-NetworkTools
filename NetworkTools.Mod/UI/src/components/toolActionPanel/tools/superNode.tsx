import React from "react";
import { GAME_BINDINGS } from "gameBindings";
import { useValue } from "cs2/api";
import { NodeSelection } from "../shared/nodeSelection";

export const SuperNodeControls: React.FC = () => {
    const selectedEntitiesBinding = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);

    return (
        <>
            <NodeSelection selectedEntities={selectedEntitiesBinding} />
        </>
    );
};
