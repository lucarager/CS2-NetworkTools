import React from "react";
import { useValue } from "cs2/api";
import { GAME_BINDINGS } from "gameBindings";
import { ParameterField } from "./parameterField";

interface TunnelFieldProps {
    paramKey: "connect.tunnel" | "parallel.tunnel" | "roadShape.tunnel";
}

/** The Tunnel toggle, greyed out while the tool holds a network that cannot go underground. */
export const TunnelField: React.FC<TunnelFieldProps> = ({ paramKey }) => {
    const available = useValue(GAME_BINDINGS.TUNNEL_AVAILABLE.binding);

    return (
        <ParameterField
            paramKey={paramKey}
            disabled={!available}
            tooltip={
                available
                    ? "NetworkTools.UI.Common.TunnelTooltip"
                    : "NetworkTools.UI.Common.TunnelUnavailableTooltip"
            }
        />
    );
};
