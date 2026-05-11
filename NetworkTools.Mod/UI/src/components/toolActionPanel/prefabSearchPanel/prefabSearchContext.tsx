import React, { createContext, useContext, useState, useCallback } from "react";

type PrefabSearchContextType = {
    isOpen: boolean;
    activeKey: string | null;
    open: (key: string) => void;
    close: () => void;
};

const PrefabSearchContext = createContext<PrefabSearchContextType>({
    isOpen: false,
    activeKey: null,
    open: () => {},
    close: () => {},
});

export const usePrefabSearch = () => useContext(PrefabSearchContext);

export const PrefabSearchProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [isOpen, setIsOpen] = useState(false);
    const [activeKey, setActiveKey] = useState<string | null>(null);

    const open = useCallback((key: string) => {
        setActiveKey(key);
        setIsOpen(true);
    }, []);

    const close = useCallback(() => {
        setActiveKey(null);
        setIsOpen(false);
    }, []);

    return (
        <PrefabSearchContext.Provider value={{ isOpen, activeKey, open, close }}>
            {children}
        </PrefabSearchContext.Provider>
    );
};
