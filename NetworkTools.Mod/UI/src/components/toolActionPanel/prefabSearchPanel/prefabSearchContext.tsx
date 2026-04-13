import React, { createContext, useContext, useState, useCallback } from "react";

type PrefabSearchContextType = {
    isOpen: boolean;
    open: () => void;
    close: () => void;
};

const PrefabSearchContext = createContext<PrefabSearchContextType>({
    isOpen: false,
    open: () => {},
    close: () => {},
});

export const usePrefabSearch = () => useContext(PrefabSearchContext);

export const PrefabSearchProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [isOpen, setIsOpen] = useState(false);
    const open = useCallback(() => setIsOpen(true), []);
    const close = useCallback(() => setIsOpen(false), []);

    return (
        <PrefabSearchContext.Provider value={{ isOpen, open, close }}>
            {children}
        </PrefabSearchContext.Provider>
    );
};
