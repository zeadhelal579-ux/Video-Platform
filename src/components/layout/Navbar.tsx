// File: Navbar.tsx
import { Link, NavLink, useNavigate } from "react-router-dom";
import { useAuth } from "../../hooks/useAuth";
import { isDemoMode } from "../../demo/config";
import { Button } from "../ui/Button";

export function Navbar() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  async function handleLogout() {
    await logout();
    navigate("/");
  }

  return (
    <header className="sticky top-0 z-10 border-b border-border bg-canvas/90 backdrop-blur-sm">
      <nav aria-label="Main" className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-x-4 gap-y-2 px-4 py-3 sm:px-6">
        <Link to="/" className="flex items-center gap-2 font-display text-lg font-bold tracking-tight text-ink">
          <span>Video<span className="text-accent">Platform</span></span>
          {isDemoMode && <span className="rounded border border-accent/30 px-1.5 py-0.5 text-xs font-medium text-accent">Demo</span>}
        </Link>

        {user && (
          <div className="order-last flex w-full items-center gap-1 sm:order-none sm:w-auto">
            <NavItem to="/dashboard" label="Dashboard" />
            <NavItem to="/upload" label="Upload" />
          </div>
        )}

        <div className="flex items-center gap-3">
          {user ? (
            <>
              <span className="hidden text-sm text-ink-faint sm:inline">{user.email}</span>
              <Button variant="ghost" onClick={handleLogout}>
                Log out
              </Button>
            </>
          ) : (
            <>
              <Link to="/login" className="text-sm font-medium text-ink-muted hover:text-ink">
                Log in
              </Link>
              {!isDemoMode && <Link to="/register" className="rounded-md bg-accent px-3 py-2 text-sm font-semibold text-accent-ink">
                Sign up
              </Link>}
            </>
          )}
        </div>
      </nav>
    </header>
  );
}

function NavItem({ to, label }: { to: string; label: string }) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        `rounded-md px-3 py-2 text-sm font-medium transition-colors ${
          isActive ? "text-ink" : "text-ink-faint hover:text-ink"
        }`
      }
    >
      {label}
    </NavLink>
  );
}
