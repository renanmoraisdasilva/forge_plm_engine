import type { ButtonHTMLAttributes, ReactNode } from 'react';

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost';

const VARIANTS: Record<Variant, string> = {
  primary:
    'border-transparent bg-primary text-white hover:bg-primary-hover active:bg-primary-active',
  secondary:
    'border-line-strong bg-surface text-ink hover:bg-canvas',
  danger:
    'border-danger-line bg-surface text-danger hover:bg-danger-bg',
  ghost: 'border-transparent bg-transparent text-ink-subtle hover:bg-muted hover:text-ink',
};

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  children: ReactNode;
}

export function Button({ variant = 'secondary', className = '', children, ...rest }: ButtonProps) {
  return (
    <button
      type="button"
      className={`inline-flex h-7 items-center justify-center gap-1.5 rounded border px-2.5 text-[12px] font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-45 ${VARIANTS[variant]} ${className}`}
      {...rest}
    >
      {children}
    </button>
  );
}
