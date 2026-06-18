// These types mirror the API's DTOs. Enums are serialized as their string names by the backend.

export type UserRole = "Admin" | "Member" | "Viewer";

export type TaskStatus = "Todo" | "InProgress" | "InReview" | "Done";

export interface User {
  id: string;
  email: string;
  fullName: string;
  role: UserRole;
  tenantId: string;
  tenantName: string;
  tenantSlug: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  user: User;
}

export interface ProjectSummary {
  id: string;
  name: string;
  description: string | null;
  createdByName: string;
  createdAt: string;
  taskCount: number;
  openTaskCount: number;
}

export interface ProjectDetail {
  id: string;
  name: string;
  description: string | null;
  createdByName: string;
  createdAt: string;
  tasks: Task[];
}

export interface Task {
  id: string;
  projectId: string;
  title: string;
  description: string | null;
  status: TaskStatus;
  assigneeUserId: string | null;
  assigneeName: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface Member {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
  createdAt: string;
}

export const TASK_STATUSES: TaskStatus[] = ["Todo", "InProgress", "InReview", "Done"];

export const STATUS_LABELS: Record<TaskStatus, string> = {
  Todo: "To Do",
  InProgress: "In Progress",
  InReview: "In Review",
  Done: "Done",
};
