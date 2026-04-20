import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Mock localStorage before imports
const localStorageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: (key: string) => store[key] || null,
    setItem: (key: string, value: string) => { store[key] = value; },
    removeItem: (key: string) => { delete store[key]; },
    clear: () => { store = {}; },
  };
})();
Object.defineProperty(globalThis, 'localStorage', { value: localStorageMock });

// Mock crypto.randomUUID
Object.defineProperty(globalThis, 'crypto', {
  value: {
    randomUUID: () => '12345678-1234-1234-1234-123456789abc',
  },
});

// Import after mocking
import { apiClient, ApiError } from '../client';

describe('ApiClient', () => {
  beforeEach(() => {
    localStorageMock.clear();
    apiClient.clearApiKey();
    vi.restoreAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('API key management', () => {
    it('should return null when no API key is set', () => {
      expect(apiClient.getApiKey()).toBeNull();
    });

    it('should set and get API key', () => {
      apiClient.setApiKey('pk_test_abc123');
      expect(apiClient.getApiKey()).toBe('pk_test_abc123');
    });

    it('should persist API key to localStorage', () => {
      apiClient.setApiKey('pk_test_abc123');
      expect(localStorageMock.getItem('payflow_api_key')).toBe('pk_test_abc123');
    });

    it('should clear API key', () => {
      apiClient.setApiKey('pk_test_abc123');
      apiClient.clearApiKey();
      expect(apiClient.getApiKey()).toBeNull();
      expect(localStorageMock.getItem('payflow_api_key')).toBeNull();
    });
  });

  describe('Mode detection', () => {
    it('should default to test mode', () => {
      expect(apiClient.getMode()).toBe('test');
    });

    it('should detect test mode from pk_test_ prefix', () => {
      apiClient.setApiKey('pk_test_abc123');
      expect(apiClient.getMode()).toBe('test');
    });

    it('should detect live mode from pk_live_ prefix', () => {
      apiClient.setApiKey('pk_live_abc123');
      expect(apiClient.getMode()).toBe('live');
    });
  });

  describe('Idempotency key generation', () => {
    it('should generate idempotency key with idem_ prefix on mutations', async () => {
      const fetchSpy = vi.spyOn(globalThis, 'fetch').mockImplementation(() =>
        Promise.resolve(new Response(JSON.stringify({ id: 'pay_test', status: 'pending', amount: 100, currency: 'GBP' }), { status: 200 }))
      );

      apiClient.setApiKey('pk_test_abc123');
      await apiClient.createPayment({ amount: 100, currency: 'GBP' });

      const calls = fetchSpy.mock.calls;
      const lastCall = calls[calls.length - 1];
      const headers = lastCall[1]?.headers as Record<string, string>;
      expect(headers['Idempotency-Key']).toMatch(/^idem_[a-f0-9]+$/);
      expect(headers['Idempotency-Key']).toMatch(/^idem_/);
    });
  });

  describe('ApiError', () => {
    it('should create ApiError with status', () => {
      const error = new ApiError(404);
      expect(error.status).toBe(404);
      expect(error.name).toBe('ApiError');
      expect(error.message).toBe('API Error: 404');
    });

    it('should create ApiError with problem details', () => {
      const problemDetails = { detail: 'Resource not found', status: 404 };
      const error = new ApiError(404, problemDetails);
      expect(error.status).toBe(404);
      expect(error.problemDetails).toEqual(problemDetails);
      expect(error.message).toBe('Resource not found');
    });
  });

  describe('API requests', () => {
    it('should send Authorization header with API key', async () => {
      const fetchSpy = vi.spyOn(globalThis, 'fetch').mockImplementation(() =>
        Promise.resolve(new Response(JSON.stringify({ id: 'pay_123', status: 'completed', amount: 100, currency: 'GBP' }), { status: 200 }))
      );

      apiClient.setApiKey('pk_test_abc123');
      await apiClient.getPayment('pay_123');

      const headers = fetchSpy.mock.calls[0][1]?.headers as Record<string, string>;
      expect(headers['Authorization']).toBe('Bearer pk_test_abc123');
    });

    it('should throw ApiError on non-ok response', async () => {
      vi.spyOn(globalThis, 'fetch').mockImplementation(() =>
        Promise.resolve(new Response(JSON.stringify({ detail: 'Not found' }), { status: 404 }))
      );

      apiClient.setApiKey('pk_test_abc123');
      await expect(apiClient.getPayment('pay_123')).rejects.toThrow(ApiError);
      await expect(apiClient.getPayment('pay_123')).rejects.toMatchObject({ status: 404 });
    });

    it('should handle 204 No Content response', async () => {
      vi.spyOn(globalThis, 'fetch').mockImplementation(() =>
        Promise.resolve(new Response(null, { status: 204 }))
      );

      apiClient.setApiKey('pk_test_abc123');
      const result = await apiClient.deleteWebhookEndpoint('wh_123');
      expect(result).toBeUndefined();
    });
  });
});
