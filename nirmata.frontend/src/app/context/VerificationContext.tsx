import { createContext, useContext, useState, useMemo } from "react";

interface VerificationContextValue {
    checkedUatIds: Set<string>;
    setCheckedUatIds: React.Dispatch<React.SetStateAction<Set<string>>>;
}

const VerificationContext = createContext<VerificationContextValue>({
    checkedUatIds: new Set(),
    setCheckedUatIds: () => {},
});

export function VerificationProvider({ children }: { children: React.ReactNode }) {
    const [checkedUatIds, setCheckedUatIds] = useState<Set<string>>(new Set());

    const value = useMemo<VerificationContextValue>(
        () => ({ checkedUatIds, setCheckedUatIds }),
        [checkedUatIds]
    );

    return (
        <VerificationContext.Provider value={value}>
            {children}
        </VerificationContext.Provider>
    );
}

export function useVerification() {
    return useContext(VerificationContext);
}
