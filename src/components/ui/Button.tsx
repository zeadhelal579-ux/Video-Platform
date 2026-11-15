// File: Button.tsx
// Purpose: Shared button styles — one accent color spent consistently
// across the whole app (index.css's design note) instead of every page
// re-deciding what a primary action looks like.
import type { ButtonHTMLAttributes, ReactNode } from "react";
import { Spinner } from "./Spinner";

type ButtonVariant = "primary" | "secondary" | "ghost" | "danger";

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  isLoading?: boolean;
  children: ReactNode;
}

const VARIANT_CLASSES: Record<ButtonVariant, string> = {
  primary: "bg-accent text-accent-ink hover:bg-accent-hover",
  secondary: "bg-surface-raised text-ink border border-border hover:border-ink-faint",
  ghost: "bg-transparent text-ink-muted hover:text-ink hover:bg-surface",
  danger: "bg-transparent text-status-failed border border-status-failed/40 hover:bg-status-failed/10",
};

export function Button({
  variant = "primary",
  isLoading = false,
  disabled,
  children,
  className = "",
  ...rest
}: ButtonProps) {
  return (
    <button
      disabled={disabled || isLoading}
      className={`inline-flex items-center justify-center gap-2 rounded-md px-4 py-2.5 text-sm font-semibold
        transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${VARIANT_CLASSES[variant]} ${className}`}
      {...rest}
    >
      {isLoading && <Spinner size={16} />}
      {children}
    </button>
  );
}
