export interface Payment {
  id: string;
  status: 'created' | 'authorised' | 'captured' | 'settled' | 'failed' | 'cancelled';
  amount: number;
  currency: string;
  mode: 'test' | 'live';
  gatewayReference?: string;
  createdAt: string;
  failureReason?: string;
  metadata?: Record<string, string>;
}

export interface Refund {
  id: string;
  paymentId: string;
  status: 'pending' | 'succeeded' | 'failed';
  amount: number;
  currency: string;
  createdAt: string;
}

export interface WebhookEndpoint {
  id: string;
  url: string;
  status: 'active' | 'disabled';
  eventTypes: string[];
  createdAt: string;
  lastRotatedAt?: string;
}

export interface SettlementBatch {
  id: string;
  settlementDate: string;
  grossAmount: number;
  feeAmount: number;
  netAmount: number;
  currency: string;
  paymentCount: number;
  status: 'pending' | 'completed' | 'failed';
  createdAt: string;
  completedAt?: string;
}

export interface ApiKey {
  id: string;
  keyPrefix: string;
  mode: 'test' | 'live';
  status: 'active' | 'revoked';
  createdAt: string;
  expiresAt?: string;
}

export interface CreatePaymentRequest {
  amount: number;
  currency: string;
  customerId: string;
  paymentMethod: {
    type: string;
    token?: string;
    last4?: string;
    brand?: string;
    expiryMonth?: string;
    expiryYear?: string;
  };
  autoCapture: boolean;
  metadata?: Record<string, string>;
}

export interface RefundPaymentRequest {
  amount: number;
  currency: string;
  reason: string;
}

export interface CreateWebhookEndpointRequest {
  url: string;
  secret: string;
  eventTypes: string[];
}

export interface UpdateWebhookEndpointRequest {
  url?: string;
  eventTypes?: string[];
}

export interface RotateWebhookSecretRequest {
  newSecret: string;
}

export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>;
}

export interface DashboardStatsResponse {
  totalPayments: number;
  totalAmount: number;
  successRate: number;
  pendingSettlements: number;
}