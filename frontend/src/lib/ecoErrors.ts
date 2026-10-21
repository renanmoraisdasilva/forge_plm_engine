import { ApiError } from '../api/http';

export interface MappedError {
  code: string;
  form: string | null;
  fields: Record<string, string>;
}

/**
 * Maps the frozen contract's validation codes to fields (contract "Codes the ECO
 * screens must handle"). The API returns a code, not a field path, so anything that
 * describes a line is attached to the lines section and shown in context there.
 */
export function mapEcoError(error: unknown): MappedError {
  if (!(error instanceof ApiError)) {
    return {
      code: 'unknown',
      form: error instanceof Error ? error.message : 'Unexpected error.',
      fields: {},
    };
  }

  const code = error.machineCode;
  const detail = error.message;
  const fields: Record<string, string> = {};
  let form: string | null = null;

  switch (code) {
    case 'blank-title':
      fields.title = 'A title is required.';
      break;
    case 'blank-actor':
      form = 'Choose an actor in the header before making a change.';
      fields.actor = form;
      break;
    case 'eco-lines-required':
      fields.lines = 'Add at least one change line.';
      break;
    case 'duplicate-target-component':
      fields.lines = 'Two lines change the same component. Split them into separate ECOs.';
      break;
    case 'target-component-required':
      fields.lines = 'Each line needs a target component.';
      break;
    case 'replacement-component-required':
      fields.lines = 'Each replacement line needs a replacement component.';
      break;
    case 'target-component-not-in-base-revision':
      fields.lines = 'The target component is not part of the base BOM revision.';
      break;
    case 'replacement-must-differ-from-target':
      fields.lines = 'The replacement must differ from the target component.';
      break;
    case 'non-positive-quantity':
      fields.lines = 'The new quantity must be a whole number greater than zero.';
      break;
    case 'component-not-active':
      fields.lines = 'The replacement component is not active and cannot be used.';
      break;
    case 'revision-limit-reached':
      form = 'This product has no revision codes left in the A\u2013Z scheme.';
      break;
    case 'rejection-reason-required':
      fields.rejectionReason = 'A rejection reason is required.';
      break;
    case 'invalid-transition':
      form =
        'This ECO changed while the page was open. Reload it before acting \u2014 the action was not sent as a valid transition.';
      break;
    case 'stale-base-revision':
      form =
        'Another ECO released a newer revision, so this one is based on an outdated BOM. Start a new ECO against the current revision.';
      break;
    case 'no-released-revision':
      fields.product = 'The product has no released BOM revision, so an ECO cannot be created against it.';
      break;
    case 'concurrency-conflict':
      form = 'Another change was saved at the same time. Reload and try again.';
      break;
    case 'conflicting-released-revision':
      form = 'Two releases raced. Reload; only one revision can be released.';
      break;
    case 'product-retired':
      form = 'This product is retired and cannot receive a released revision.';
      break;
    case 'network-error':
      form = 'The API did not respond. Check that it is running on port 5199.';
      break;
    default:
      form = `${detail || 'Request failed.'} (${code})`;
  }

  return { code, form, fields };
}
