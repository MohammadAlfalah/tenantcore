import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import type { AuthResponse, User } from "../types";
import { authApi } from "../api/auth";
import { setOnSessionExpired } from "../api/client";
import { tokenStorage } from "./tokenStorage";

interface AuthContextValue {
  user: User | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (input: {
    tenantName: string;
    fullName: string;
    email: string;
    password: string;
  }) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => tokenStorage.getUser());
  const [loading, setLoading] = useState(true);

  const applyAuth = useCallback((auth: AuthResponse) => {
    tokenStorage.set(auth.accessToken, auth.refreshToken, auth.user);
    setUser(auth.user);
  }, []);

  const clearSession = useCallback(() => {
    tokenStorage.clear();
    setUser(null);
  }, []);

  // When the API client gives up refreshing, drop the user back to the login screen.
  useEffect(() => {
    setOnSessionExpired(clearSession);
  }, [clearSession]);

  // On first load, if we have a stored session, verify it against the API (and pick up role changes).
  useEffect(() => {
    let active = true;
    (async () => {
      if (tokenStorage.getAccess()) {
        try {
          const fresh = await authApi.me();
          if (active) setUser(fresh);
        } catch {
          if (active) clearSession();
        }
      }
      if (active) setLoading(false);
    })();
    return () => {
      active = false;
    };
  }, [clearSession]);

  const login = useCallback(
    async (email: string, password: string) => {
      applyAuth(await authApi.login({ email, password }));
    },
    [applyAuth]
  );

  const register = useCallback(
    async (input: { tenantName: string; fullName: string; email: string; password: string }) => {
      applyAuth(await authApi.register(input));
    },
    [applyAuth]
  );

  const logout = useCallback(async () => {
    const refreshToken = tokenStorage.getRefresh();
    if (refreshToken) {
      try {
        await authApi.logout(refreshToken);
      } catch {
        // Best-effort server-side revocation; local state is cleared regardless.
      }
    }
    clearSession();
  }, [clearSession]);

  const value = useMemo(
    () => ({ user, loading, login, register, logout }),
    [user, loading, login, register, logout]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within an AuthProvider");
  return ctx;
}
