import type { ReactNode } from "react";
import { useAuth } from "../auth/AuthContext";
import type { UserRole } from "../types";

/**
 * Renders children only when the current user's role passes `allow`. This is purely a UX affordance
 * (hiding buttons the user can't use) — the API independently enforces the same rules, so a Viewer
 * who forges a request still gets a 403.
 */
export function RoleGate({
  allow,
  children,
  fallback = null,
}: {
  allow: (role: UserRole) => boolean;
  children: ReactNode;
  fallback?: ReactNode;
}) {
  const { user } = useAuth();
  if (!user || !allow(user.role)) return <>{fallback}</>;
  return <>{children}</>;
}
