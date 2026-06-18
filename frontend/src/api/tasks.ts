import { api } from "./client";
import type { Task, TaskStatus } from "../types";

export interface TaskInput {
  title: string;
  description?: string | null;
  assigneeUserId?: string | null;
  status: TaskStatus;
}

export const tasksApi = {
  async create(projectId: string, input: TaskInput): Promise<Task> {
    const { data } = await api.post<Task>(`/api/projects/${projectId}/tasks`, input);
    return data;
  },

  async update(taskId: string, input: TaskInput): Promise<Task> {
    const { data } = await api.put<Task>(`/api/tasks/${taskId}`, input);
    return data;
  },

  async updateStatus(taskId: string, status: TaskStatus): Promise<Task> {
    const { data } = await api.patch<Task>(`/api/tasks/${taskId}/status`, { status });
    return data;
  },

  async remove(taskId: string): Promise<void> {
    await api.delete(`/api/tasks/${taskId}`);
  },
};
