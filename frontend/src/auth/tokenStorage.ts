import type { User } from "../types";

// Tokens live in localStorage so a refresh of the page keeps the session. The access token is sent
// on every request; the refresh token is only sent to /api/auth/refresh.
const ACCESS = "tc.accessToken";
const REFRESH = "tc.refreshToken";
const USER = "tc.user";

export const tokenStorage = {
  getAccess: () => localStorage.getItem(ACCESS),
  getRefresh: () => localStorage.getItem(REFRESH),

  getUser(): User | null {
    const raw = localStorage.getItem(USER);
    return raw ? (JSON.parse(raw) as User) : null;
  },

  set(accessToken: string, refreshToken: string, user: User) {
    localStorage.setItem(ACCESS, accessToken);
    localStorage.setItem(REFRESH, refreshToken);
    localStorage.setItem(USER, JSON.stringify(user));
  },

  setAccess(accessToken: string) {
    localStorage.setItem(ACCESS, accessToken);
  },

  clear() {
    localStorage.removeItem(ACCESS);
    localStorage.removeItem(REFRESH);
    localStorage.removeItem(USER);
  },
};
