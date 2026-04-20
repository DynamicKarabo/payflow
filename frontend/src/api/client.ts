import type {
  Payment,
  Refund,
  WebhookEndpoint,
  SettlementBatch,
  CreatePaymentRequest,
  RefundPaymentRequest,
  CreateWebhookEndpointRequest,
  UpdateWebhookEndpointRequest,
  RotateWebhookSecretRequest,
  DashboardStatsResponse,
  ProblemDetails,
} from '../types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5062';

class ApiError extends Error {
  status: number;
  problemDetails?: ProblemDetails;

  constructor(
    status: number,
    problemDetails?: ProblemDetails
  ) {
    super(problemDetails?.detail || `API Error: ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.problemDetails = problemDetails;
  }
}

class ApiClient {
  private apiKey: string | null = null;
  private mode: 'test' | 'live' = 'test';

  setApiKey(key: string) {
    this.apiKey = key;
    this.mode = key.startsWith('pk_live_') ? 'live' : 'test';
    localStorage.setItem('payflow_api_key', key);
    localStorage.setItem('payflow_mode', this.mode);
  }

  getApiKey(): string | null {
    if (!this.apiKey) {
      this.apiKey = localStorage.getItem('payflow_api_key');
      const savedMode = localStorage.getItem('payflow_mode');
      if (savedMode === 'live' || savedMode === 'test') {
        this.mode = savedMode;
      }
    }
    return this.apiKey;
  }

  getMode(): 'test' | 'live' {
    return this.mode;
  }

  clearApiKey() {
    this.apiKey = null;
    this.mode = 'test';
    localStorage.removeItem('payflow_api_key');
    localStorage.removeItem('payflow_mode');
  }

  private async request<T>(
    path: string,
    options: RequestInit = {},
    idempotencyKey?: string
  ): Promise<T> {
    const apiKey = this.getApiKey();
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...(options.headers as Record<string, string>),
    };

    if (apiKey) {
      headers['Authorization'] = `Bearer ${apiKey}`;
    }

    if (idempotencyKey) {
      headers['Idempotency-Key'] = idempotencyKey;
    }

    const response = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      headers,
    });

    if (!response.ok) {
      let problemDetails: ProblemDetails | undefined;
      try {
        problemDetails = await response.json();
      } catch {
        // Response might not be JSON
      }
      throw new ApiError(response.status, problemDetails);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return response.json();
  }

  // Payment endpoints
  async createPayment(request: CreatePaymentRequest): Promise<Payment> {
    const idempotencyKey = this.generateIdempotencyKey();
    return this.request<Payment>('/v1/payments', {
      method: 'POST',
      body: JSON.stringify(request),
    }, idempotencyKey);
  }

  async getPayment(id: string): Promise<Payment> {
    return this.request<Payment>(`/v1/payments/${id}`);
  }

  async capturePayment(id: string): Promise<Payment> {
    return this.request<Payment>(`/v1/payments/${id}/capture`, {
      method: 'POST',
    });
  }

  async refundPayment(id: string, request: RefundPaymentRequest): Promise<Refund> {
    const idempotencyKey = this.generateIdempotencyKey();
    return this.request<Refund>(`/v1/payments/${id}/refund`, {
      method: 'POST',
      body: JSON.stringify(request),
    }, idempotencyKey);
  }

  async cancelPayment(id: string): Promise<Payment> {
    return this.request<Payment>(`/v1/payments/${id}/cancel`, {
      method: 'POST',
    });
  }

  async failPayment(id: string, reason: string): Promise<Payment> {
    return this.request<Payment>(`/v1/payments/${id}/fail`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    });
  }

  // Webhook endpoints
  async getWebhookEndpoints(): Promise<WebhookEndpoint[]> {
    return this.request<WebhookEndpoint[]>('/v1/webhook-endpoints');
  }

  async createWebhookEndpoint(request: CreateWebhookEndpointRequest): Promise<WebhookEndpoint> {
    return this.request<WebhookEndpoint>('/v1/webhook-endpoints', {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  async updateWebhookEndpoint(id: string, request: UpdateWebhookEndpointRequest): Promise<WebhookEndpoint> {
    return this.request<WebhookEndpoint>(`/v1/webhook-endpoints/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request),
    });
  }

  async deleteWebhookEndpoint(id: string): Promise<void> {
    return this.request<void>(`/v1/webhook-endpoints/${id}`, {
      method: 'DELETE',
    });
  }

  async rotateWebhookSecret(id: string, request: RotateWebhookSecretRequest): Promise<WebhookEndpoint> {
    return this.request<WebhookEndpoint>(`/v1/webhook-endpoints/${id}/rotate-secret`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
  }

  // Settlement endpoints
  async getSettlements(fromDate?: string, toDate?: string): Promise<SettlementBatch[]> {
    const params = new URLSearchParams();
    if (fromDate) params.append('fromDate', fromDate);
    if (toDate) params.append('toDate', toDate);
    const query = params.toString();
    return this.request<SettlementBatch[]>(`/v1/settlements${query ? `?${query}` : ''}`);
  }

  async getSettlement(id: string): Promise<SettlementBatch> {
    return this.request<SettlementBatch>(`/v1/settlements/${id}`);
  }

  // Dashboard endpoints
  async getDashboardStats(): Promise<DashboardStatsResponse> {
    return this.request<DashboardStatsResponse>('/v1/dashboard/stats');
  }

  // Health check
  async healthCheck(): Promise<{ status: string }> {
    return this.request<{ status: string }>('/health/ready');
  }

  private generateIdempotencyKey(): string {
    return 'idem_' + crypto.randomUUID().replace(/-/g, '').slice(0, 56);
  }
}

export const apiClient = new ApiClient();
export { ApiError };