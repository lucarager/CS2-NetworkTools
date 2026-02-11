import colorConverter from "postcss-color-converter";

export const syntax = "postcss-scss";
export const plugins = [
    colorConverter({
        outputColorFormat: "rgb",
        ignore: ["hex"],
    }),
];
