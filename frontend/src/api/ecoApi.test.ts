import { describe, expect, it, vi } from 'vitest';
import { LiveEcoApi } from './ecoApi';
import type { HttpClient } from './http';
import type { CreateEcoRequest, EcoDetail, PagedResponse, EcoSummary } from './types';

/**
 * The mock adapter is gone, so these pin the one thing the screens now depend on: that each
 * action calls the endpoint and method in docs/ECO-API-CONTRACT.md with the right body.
 */
const id = '11111111-1111-1111-1111-111111111111';

function fakeHttp() {
  const get = vi.fn().mockResolvedValue({} as PagedResponse<EcoSummary>);
  const post = vi.fn().mockResolvedValue({} as EcoDetail);
  const put = vi.fn().mockResolvedValue({} as EcoDetail);
  return { client: { get, post, put } as unknown as HttpClient, get, post, put };
}

const line = {
  changeType: 'ReplaceComponent' as const,
  targetComponentId: '22222222-2222-2222-2222-222222222222',
  replacementComponentId: '33333333-3333-3333-3333-333333333333',
  newQuantity: null,
  reason: null,
};

const createRequest: CreateEcoRequest = {
  productId: '44444444-4444-4444-4444-444444444444',
  title: 'Replace the motherboard',
  reasonForChange: 'Obsolete part',
  impactAssessment: null,
  requestedEffectiveAtUtc: null,
  actor: 'Alex Morgan',
  lines: [line],
};

describe('LiveEcoApi — contract endpoints', () => {
  it('lists ECOs with the filters that are set', async () => {
    const { client, get } = fakeHttp();
    const params = { status: 'Approved' as const, page: 2, pageSize: 10, search: '' };
    await new LiveEcoApi(client).listEcos(params);

    expect(get).toHaveBeenCalledWith('/ecos', params);
  });

  it('reads one ECO by id', async () => {
    const { client, get } = fakeHttp();
    await new LiveEcoApi(client).getEco(id);

    expect(get).toHaveBeenCalledWith(`/ecos/${id}`);
  });

  it('creates a draft with the full request body', async () => {
    const { client, post } = fakeHttp();
    await new LiveEcoApi(client).createEco(createRequest);

    expect(post).toHaveBeenCalledWith('/ecos', createRequest);
  });

  it('updates a draft with PUT and no productId or actor', async () => {
    const { client, put } = fakeHttp();
    const update = {
      title: 'Replace the motherboard',
      reasonForChange: 'Obsolete part',
      impactAssessment: null,
      requestedEffectiveAtUtc: null,
      lines: [line],
    };
    await new LiveEcoApi(client).updateEco(id, update);

    expect(put).toHaveBeenCalledWith(`/ecos/${id}`, update);
  });

  it('submits with only the actor', async () => {
    const { client, post } = fakeHttp();
    await new LiveEcoApi(client).submitEco(id, 'Alex Morgan');

    expect(post).toHaveBeenCalledWith(`/ecos/${id}/submit`, { actor: 'Alex Morgan' });
  });

  it('approves with actor and optional comment', async () => {
    const { client, post } = fakeHttp();
    await new LiveEcoApi(client).approveEco(id, { actor: 'Jordan Lee', comment: null });

    expect(post).toHaveBeenCalledWith(`/ecos/${id}/approve`, { actor: 'Jordan Lee', comment: null });
  });

  it('rejects with a required reason', async () => {
    const { client, post } = fakeHttp();
    await new LiveEcoApi(client).rejectEco(id, { actor: 'Jordan Lee', rejectionReason: 'Out of scope' });

    expect(post).toHaveBeenCalledWith(`/ecos/${id}/reject`, {
      actor: 'Jordan Lee',
      rejectionReason: 'Out of scope',
    });
  });

  it('releases with immediate-effectivity confirmation', async () => {
    const { client, post } = fakeHttp();
    await new LiveEcoApi(client).releaseEco(id, {
      actor: 'Alex Morgan',
      confirmImmediateEffectivity: true,
    });

    expect(post).toHaveBeenCalledWith(`/ecos/${id}/release`, {
      actor: 'Alex Morgan',
      confirmImmediateEffectivity: true,
    });
  });

  it('reads the audit trail filtered to one ECO', async () => {
    const { client, get } = fakeHttp();
    const params = {
      entityType: 'EngineeringChangeOrder' as const,
      entityId: id,
      pageSize: 200,
    };
    await new LiveEcoApi(client).listAudit(params);

    expect(get).toHaveBeenCalledWith('/audit', params);
  });
});
