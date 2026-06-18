import { api } from "./client";
import type { AuthResponse, User } from "../types";

export const authApi = {
  async register(input: {
    tenantName: string;
    fullName: string;
    email: string;
    password: string;
  }): Promise<AuthResponse> {
    const { data } = await api.post<AuthResponse>("/api/auth/register", input);
    return data;
  },

  async login(input: { email: string; password: string }): Promise<AuthResponse> {
    const { data } = await api.post<AuthResponse>("/api/auth/login", input);
    return data;
  },

  async me(): Promise<User> {
    const { data } = await api.get<User>("/api/auth/me");
    return data;
  },

  async logout(refreshToken: string): Promise<void> {
    await api.post("/api/auth/logout", { refreshToken });
  },
};
