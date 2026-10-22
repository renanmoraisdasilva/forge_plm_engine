import { describe, expect, it } from 'vitest';
import { ApiError } from '../api/http';
import { mapEcoError } from './ecoErrors';

type Field = 'title' | 'actor' | 'lines' | 'rejectionReason' | 'product';

const fieldCases: Array<[string, Field]> = [
  ['blank-title', 'title'],
  ['blank-actor', 'actor'],
  ['eco-lines-required', 'lines'],
  ['duplicate-target-component', 'lines'],
  ['target-component-required', 'lines'],
  ['replacement-component-required', 'lines'],
  ['target-component-not-in-base-revision', 'lines'],
  ['replacement-must-differ-from-target', 'lines'],
  ['non-positive-quantity', 'lines'],
  ['component-not-active', 'lines'],
  ['rejection-reason-required', 'rejectionReason'],
  ['no-released-revision', 'product'],
];

const formCases = [
  'revision-limit-reached',
  'invalid-transition',
  'stale-base-revision',
  'concurrency-conflict',
  'conflicting-released-revision',
  'product-retired',
  'network-error',
];

describe('mapEcoError', () => {
  it.each(fieldCases)('maps %s onto the %s field', (code, field) => {
    const mapped = mapEcoError(new ApiError(400, { title: 'Validation failed', code }));

    expect(mapped.code).toBe(code);
    expect(mapped.fields[field]).toBeTruthy();
  });

  it.each(formCases)('maps %s onto the form', (code) => {
    const mapped = mapEcoError(new ApiError(409, { title: 'Conflict', code }));

    expect(mapped.code).toBe(code);
    expect(mapped.form).toBeTruthy();
  });

  it('keeps an unknown machine code visible', () => {
    const mapped = mapEcoError(new ApiError(418, { title: 'Teapot', code: 'mystery-code' }));

    expect(mapped).toEqual({
      code: 'mystery-code',
      form: 'Teapot (mystery-code)',
      fields: {},
    });
  });

  it('preserves the message of a non-ApiError Error', () => {
    const mapped = mapEcoError(new Error('The network is down'));

    expect(mapped).toEqual({
      code: 'unknown',
      form: 'The network is down',
      fields: {},
    });
  });

  it('uses a stable fallback for a non-Error rejection', () => {
    const mapped = mapEcoError('unexpected');

    expect(mapped).toEqual({
      code: 'unknown',
      form: 'Unexpected error.',
      fields: {},
    });
  });
});
