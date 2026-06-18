import type { UserRole } from "../types";

/** Admin or Member: may create/manage tasks (and, for Admin, projects/members). */
export const canEdit = (role: UserRole | undefined): boolean =>
  role === "Admin" || role === "Member";

/** Admin: full control including project and member management. */
export const isAdmin = (role: UserRole | undefined): boolean => role === "Admin";

export const ROLE_LABELS: Record<UserRole, string> = {
  Admin: "Admin",
  Member: "Member",
  Viewer: "Viewer",
};

export const ROLE_BADGE_CLASSES: Record<UserRole, string> = {
  Admin: "bg-brand-100 text-brand-700",
  Member: "bg-emerald-100 text-emerald-700",
  Viewer: "bg-slate-200 text-slate-700",
};
