import { api } from "./client";
import type { ProjectDetail, ProjectSummary } from "../types";

export const projectsApi = {
  async list(): Promise<ProjectSummary[]> {
    const { data } = await api.get<ProjectSummary[]>("/api/projects");
    return data;
  },

  async get(id: string): Promise<ProjectDetail> {
    const { data } = await api.get<ProjectDetail>(`/api/projects/${id}`);
    return data;
  },

  async create(input: { name: string; description?: string | null }): Promise<ProjectDetail> {
    const { data } = await api.post<ProjectDetail>("/api/projects", input);
    return data;
  },

  async update(
    id: string,
    input: { name: string; description?: string | null }
  ): Promise<ProjectDetail> {
    const { data } = await api.put<ProjectDetail>(`/api/projects/${id}`, input);
    return data;
  },

  async remove(id: string): Promise<void> {
    await api.delete(`/api/projects/${id}`);
  },
};
