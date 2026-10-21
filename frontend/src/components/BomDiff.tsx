import type { BomLine, EcoLine } from '../api/types';

interface DiffRow {
  key: string;
  beforePart: string;
  beforeName: string;
  beforeQuantity: number | null;
  afterPart: string;
  afterName: string;
  afterQuantity: number | null;
  changeLabel: string | null;
}

/**
 * Before/after comparison of the proposed change against the ECO's base revision.
 * Reads, never computes, the resulting configuration: the base BOM comes from the
 * revision endpoint and the replacement/quantity comes from the ECO line.
 */
export function BomDiff({ baseLines, changes }: { baseLines: BomLine[]; changes: EcoLine[] }) {
  const rows: DiffRow[] = [];

  for (const line of baseLines) {
    const change = changes.find((candidate) => candidate.targetComponentId === line.componentId);
    if (!change) {
      rows.push({
        key: line.id,
        beforePart: line.component.partNumber,
        beforeName: line.component.name,
        beforeQuantity: line.quantity,
        afterPart: line.component.partNumber,
        afterName: line.component.name,
        afterQuantity: line.quantity,
        changeLabel: null,
      });
      continue;
    }

    const afterPart = change.replacement ?? change.target;
    rows.push({
      key: change.id,
      beforePart: line.component.partNumber,
      beforeName: line.component.name,
      beforeQuantity: line.quantity,
      afterPart: afterPart.partNumber,
      afterName: afterPart.name,
      afterQuantity: change.changeType === 'ChangeQuantity' ? change.newQuantity : line.quantity,
      changeLabel: change.changeType === 'ReplaceComponent' ? 'Replace' : 'Quantity',
    });
  }

  // Defensive: an ECO line whose target is not in the base BOM is invalid server-side,
  // but the comparison should still render the proposed line rather than hide it.
  for (const change of changes) {
    if (rows.some((row) => row.key === change.id)) continue;
    if (baseLines.some((line) => line.componentId === change.targetComponentId)) continue;
    rows.push({
      key: change.id,
      beforePart: change.target.partNumber,
      beforeName: change.target.name,
      beforeQuantity: null,
      afterPart: change.replacement?.partNumber ?? change.target.partNumber,
      afterName: change.replacement?.name ?? change.target.name,
      afterQuantity: change.newQuantity,
      changeLabel: change.changeType === 'ReplaceComponent' ? 'Replace' : 'Quantity',
    });
  }

  const changedCount = rows.filter((row) => row.changeLabel !== null).length;

  return (
    <div>
      <div className="flex items-center justify-between border-b border-line px-3 py-1.5 text-[11px] text-ink-muted">
        <span>
          {changedCount} of {rows.length} base lines change
        </span>
        <span className="flex items-center gap-2">
          <span className="inline-block h-2 w-2 rounded-full bg-primary" aria-hidden="true" />
          Proposed change
        </span>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-[13px]">
          <thead>
            <tr>
              <th className="table-head px-3">Part number</th>
              <th className="table-head px-3">Component</th>
              <th className="table-head px-3 text-right">Qty before</th>
              <th className="table-head px-3">Part number after</th>
              <th className="table-head px-3">Component after</th>
              <th className="table-head px-3 text-right">Qty after</th>
              <th className="table-head px-3">Change</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => {
              const changed = row.changeLabel !== null;
              return (
                <tr
                  key={row.key}
                  className={`h-[32px] border-b border-line ${changed ? 'bg-primary-tint/40' : ''}`}
                >
                  <td className={`px-3 ${changed ? 'border-l-2 border-primary' : ''}`}>
                    <span className={`mono ${changed ? 'font-semibold text-ink' : 'text-ink-subtle'}`}>
                      {row.beforePart}
                    </span>
                  </td>
                  <td className="px-3 text-ink-subtle">{row.beforeName}</td>
                  <td className="px-3 text-right tabular-nums text-ink-subtle">
                    {row.beforeQuantity ?? '\u2014'}
                  </td>
                  <td className="px-3">
                    <span className={`mono ${changed ? 'font-semibold text-primary-active' : 'text-ink-subtle'}`}>
                      {row.afterPart}
                    </span>
                  </td>
                  <td className={`px-3 ${changed ? 'text-ink' : 'text-ink-subtle'}`}>{row.afterName}</td>
                  <td className={`px-3 text-right tabular-nums ${changed ? 'font-semibold text-ink' : 'text-ink-subtle'}`}>
                    {row.afterQuantity ?? '\u2014'}
                  </td>
                  <td className="px-3">
                    {row.changeLabel ? (
                      <span className="inline-flex h-5 items-center rounded-[3px] border border-info-line bg-info-bg px-1.5 text-[11px] font-semibold text-info-text">
                        {row.changeLabel}
                      </span>
                    ) : (
                      <span className="text-ink-muted">Unchanged</span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
