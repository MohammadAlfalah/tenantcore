import { api } from "./client";
import type { Member, UserRole } from "../types";

export const membersApi = {
  async list(): Promise<Member[]> {
    const { data } = await api.get<Member[]>("/api/members");
    return data;
  },

  async create(input: {
    fullName: string;
    email: string;
    password: string;
    role: UserRole;
  }): Promise<Member> {
    const { data } = await api.post<Member>("/api/members", input);
    return data;
  },

  async update(id: string, input: { fullName: string; role: UserRole }): Promise<Member> {
    const { data } = await api.put<Member>(`/api/members/${id}`, input);
    return data;
  },

  async remove(id: string): Promise<void> {
    await api.delete(`/api/members/${id}`);
  },
};
