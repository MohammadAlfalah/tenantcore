import axios, {
  AxiosError,
  type AxiosInstance,
  type InternalAxiosRequestConfig,
} from "axios";
import type { AuthResponse } from "../types";
import { tokenStorage } from "../auth/tokenStorage";

// Base URL is empty by default: the app calls relative `/api/*` paths that are proxied to the API
// (by Vite in dev, by nginx in Docker). Override with VITE_API_BASE_URL if you ever need to.
const baseURL = import.meta.env.VITE_API_BASE_URL ?? "";

export const api: AxiosInstance = axios.create({ baseURL });

// A bare client with no interceptors, used only to refresh — so refreshing can never recurse.
const refreshClient = axios.create({ baseURL });

// Called when the session can no longer be recovered (refresh failed). The AuthProvider registers a
// handler here so it can clear React state and bounce the user to /login.
let onSessionExpired: (() => void) | null = null;
export const setOnSessionExpired = (handler: () => void) => {
  onSessionExpired = handler;
};

// Attach the access token to every outgoing request.
api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = tokenStorage.getAccess();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Single-flight refresh: concurrent 401s share one refresh call, then all retry.
let refreshPromise: Promise<string> | null = null;

async function refreshAccessToken(): Promise<string> {
  const refreshToken = tokenStorage.getRefresh();
  if (!refreshToken) throw new Error("No refresh token");

  const { data } = await refreshClient.post<AuthResponse>("/api/auth/refresh", {
    refreshToken,
  });
  tokenStorage.set(data.accessToken, data.refreshToken, data.user);
  return data.accessToken;
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as (InternalAxiosRequestConfig & { _retried?: boolean }) | undefined;

    const isAuthCall = original?.url?.includes("/api/auth/login") ||
      original?.url?.includes("/api/auth/refresh") ||
      original?.url?.includes("/api/auth/register");

    if (error.response?.status === 401 && original && !original._retried && !isAuthCall) {
      original._retried = true;
      try {
        // The first failing request kicks off the refresh; the rest await the same promise.
        refreshPromise ??= refreshAccessToken().finally(() => {
          refreshPromise = null;
        });
        const newToken = await refreshPromise;
        original.headers.Authorization = `Bearer ${newToken}`;
        return api(original);
      } catch {
        tokenStorage.clear();
        onSessionExpired?.();
      }
    }

    return Promise.reject(error);
  }
);

/** Extracts a human-readable message from an API error (ProblemDetails title or validation errors). */
export function apiErrorMessage(error: unknown, fallback = "Something went wrong."): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as
      | { title?: string; errors?: Record<string, string[]> }
      | undefined;
    if (data?.errors) {
      const first = Object.values(data.errors).flat()[0];
      if (first) return first;
    }
    if (data?.title) return data.title;
    if (error.message) return error.message;
  }
  return fallback;
}
