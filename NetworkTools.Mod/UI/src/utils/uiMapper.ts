import { ModuleRegistry } from "cs2/modding";

const mapper: Record<string, string> = {
    focusKey: "UniqueFocusKey | null",
    src: "string",
    selected: "boolean",
    multiSelect: "boolean",
    disabled: "boolean",
    tooltip: "string | JSX.Element | null",
    uiTag: "string",
    className: "string",
    children: "string | JSX.Element | JSX.Element[]",
    onSelect: "(x: any) => any",
};
let resultString = "";

export const mapperFn = (moduleRegistry: ModuleRegistry) => {
    // Generate types
    for (const [key, module] of moduleRegistry.registry) {
        // Handle styles
        const fileExt = key.split(".").pop();

        switch (fileExt) {
            case "ts":
                break;
            case "tsx":
                for (const prop in module) {
                    try {
                        const obj = module[prop];
                        if (typeof obj === "function") {
                            const string: string = obj.toString();
                            if (!string.startsWith("({") || string.split("=>").length === 0) {
                                break;
                            }
                            const fn = string.split("})=>")[0].substring(2);
                            const ar = fn.split(",");

                            const map = ar.map((a) => {
                                let type = "any";
                                let optional = false;
                                // eslint-disable-next-line prefer-const
                                let [arg, value] = a.split(":");

                                if (arg.startsWith("...")) {
                                    arg = arg[4];
                                    type = "{[x: string]: any};";
                                }

                                // Arg Matching
                                if (mapper[arg]) {
                                    type = mapper[arg];
                                } else if (arg === "...c") {
                                    type = "{[x: string]: any};";
                                } else if (arg.startsWith("on")) {
                                    type = "function";
                                }

                                // Optional Matching
                                if (value && value.includes("=")) {
                                    // Optional types
                                    optional = true;
                                    const [_, defaultVal] = value.split("=");
                                    const firstChar = defaultVal[0];
                                    if (firstChar === '"') {
                                        type = "string";
                                    } else if (firstChar === "!") {
                                        type = "boolean";
                                    } else if (firstChar >= "0" && firstChar <= "9") {
                                        type = "number";
                                    }
                                    type = "number";
                                }

                                if (optional) {
                                    arg += "?";
                                }

                                return [arg, type];
                            });

                            const typeStrings = map.map((a) => a[0] + ":" + a[1] + ";");

                            resultString += `export type ${prop} = (props: {
                                    ${typeStrings.join("\n")}
                                }) => JSX.Element;\n`;
                        }
                    } catch (e) {
                        // console.error(key, prop, e);
                        break;
                    }
                }
                break;
            case "scss":
                break;
            default:
                break;
        }
    }

    console.log("resultString", resultString);
};
