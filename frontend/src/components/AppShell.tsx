import { NavLink, Outlet } from 'react-router-dom';
import { ActorSwitch } from './ActorSwitch';

function ProductsIcon() {
  return (
    <svg viewBox="0 0 20 20" aria-hidden="true" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.5">
      <path d="M3.5 6.5 10 3l6.5 3.5v7L10 17l-6.5-3.5v-7Z" strokeLinejoin="round" />
      <path d="m3.5 6.5 6.5 3.5 6.5-3.5M10 10v7" strokeLinejoin="round" />
    </svg>
  );
}

function ChangesIcon() {
  return (
    <svg viewBox="0 0 20 20" aria-hidden="true" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.5">
      <path d="M4 6h9.5L11 3.5M16 14H6.5L9 16.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

const NAV = [
  { to: '/products', label: 'Products', icon: <ProductsIcon /> },
  { to: '/ecos', label: 'Engineering Changes', icon: <ChangesIcon /> },
];

export function AppShell() {
  return (
    <div className="flex h-screen overflow-hidden bg-canvas">
      <aside className="flex w-60 shrink-0 flex-col border-r border-line bg-surface">
        <div className="flex h-14 items-center gap-2 border-b border-line px-4">
          <span className="flex h-7 w-7 items-center justify-center rounded-[3px] bg-primary text-[11px] font-bold text-white">
            FP
          </span>
          <div className="leading-tight">
            <div className="text-[14px] font-semibold tracking-[-0.01em] text-ink">Forge PLM</div>
            <div className="text-[10px] uppercase tracking-[0.08em] text-ink-muted">Product data</div>
          </div>
        </div>

        <nav aria-label="Primary" className="flex flex-col gap-0.5 p-2">
          {NAV.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `flex h-9 items-center gap-2 rounded-[3px] border-l-2 px-2.5 text-[12px] font-medium transition-colors ${
                  isActive
                    ? 'border-primary bg-primary-tint text-primary-active'
                    : 'border-transparent text-ink-subtle hover:bg-muted hover:text-ink'
                }`
              }
            >
              {item.icon}
              {item.label}
            </NavLink>
          ))}
        </nav>

        <div className="mt-auto border-t border-line px-4 py-3 text-[10px] leading-4 text-ink-muted">
          PDM / ECM prototype
          <br />
          Approval and release are separate actions.
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 shrink-0 items-center justify-between gap-4 border-b border-line-strong bg-surface px-6">
          <p className="text-[12px] text-ink-muted">Controlled product configuration and engineering changes</p>
          <ActorSwitch />
        </header>

        <main className="min-h-0 flex-1 overflow-auto">
          <div className="mx-auto w-full max-w-[1440px] px-6 py-5">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
}
