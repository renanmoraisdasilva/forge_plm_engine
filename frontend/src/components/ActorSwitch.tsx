import { ACTORS, useActor } from '../state/ActorProvider';

/**
 * D-06 actor switch. Client-side only: no endpoint exists, and the selection is sent in
 * each state-changing request body. Labelled as a demo affordance on purpose.
 */
export function ActorSwitch() {
  const { actor, setActor } = useActor();

  return (
    <div className="flex items-center gap-2" title="Demo identity, not authentication">
      <span className="text-[10px] font-semibold uppercase tracking-[0.06em] text-ink-muted">Acting as</span>
      <div role="group" aria-label="Acting as" className="flex rounded border border-line-strong bg-muted p-0.5">
        {ACTORS.map((name) => {
          const selected = name === actor;
          return (
            <button
              key={name}
              type="button"
              aria-pressed={selected}
              onClick={() => setActor(name)}
              className={`h-6 rounded-[3px] px-2 text-[12px] font-medium transition-colors ${
                selected ? 'bg-surface text-ink shadow-[0_1px_2px_rgba(15,23,42,0.08)]' : 'text-ink-muted hover:text-ink'
              }`}
            >
              {name}
            </button>
          );
        })}
      </div>
      <span className="hidden text-[10px] text-ink-muted lg:inline">demo identity</span>
    </div>
  );
}
