import React, { useState } from "react";
import { ToolActionPanel } from "components/toolActionPanel/toolActionPanel";
import { ToolSelectPanel } from "components/toolSelectPanel/toolSelectPanel";

export const Editor = () => {
    const [enabled, setIsEnabled] = useState(false);

    return (
        <div
            style={{
                display: "flex",
                position: "absolute",
                top: "60rem",
                left: "60rem",
                zIndex: 1000,
                pointerEvents: "all",
            }}>
            {<ToolSelectPanel />}
            {<ToolActionPanel />}
        </div>
    );
};
