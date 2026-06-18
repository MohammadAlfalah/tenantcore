import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { isAdmin, ROLE_BADGE_CLASSES, ROLE_LABELS } from "../lib/roles";
import { Badge, Button } from "./ui";

export function Layout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  if (!user) return null;

  const handleLogout = async () => {
    await logout();
    navigate("/login", { replace: true });
  };

  const navLinkClass = ({ isActive }: { isActive: boolean }) =>
    `rounded-lg px-3 py-2 text-sm font-medium transition ${
      isActive ? "bg-brand-50 text-brand-700" : "text-slate-600 hover:bg-slate-100"
    }`;

  return (
    <div className="min-h-screen">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
          <div className="flex items-center gap-6">
            <div className="flex items-center gap-2">
              <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-brand-600 text-sm font-bold text-white">
                TC
              </div>
              <div className="leading-tight">
                <div className="text-sm font-semibold text-slate-900">TenantCore</div>
                <div className="text-xs text-slate-500">{user.tenantName}</div>
              </div>
            </div>
            <nav className="flex items-center gap-1">
              <NavLink to="/" end className={navLinkClass}>
                Dashboard
              </NavLink>
              {isAdmin(user.role) && (
                <NavLink to="/members" className={navLinkClass}>
                  Members
                </NavLink>
              )}
            </nav>
          </div>

          <div className="flex items-center gap-3">
            <div className="hidden text-right sm:block">
              <div className="text-sm font-medium text-slate-800">{user.fullName}</div>
              <div className="text-xs text-slate-500">{user.email}</div>
            </div>
            <Badge className={ROLE_BADGE_CLASSES[user.role]}>{ROLE_LABELS[user.role]}</Badge>
            <Button variant="secondary" onClick={handleLogout}>
              Sign out
            </Button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 py-8">
        <Outlet />
      </main>
    </div>
  );
}
