import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { DashboardPage } from '../DashboardPage';
import { AuthProvider } from '../../contexts/AuthContext';

// Mock localStorage
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

// Mock crypto
Object.defineProperty(globalThis, 'crypto', {
  value: {
    randomUUID: () => '12345678-1234-1234-1234-123456789abc',
  },
});

// Mock the apiClient
vi.mock('../../api/client', () => ({
  apiClient: {
    getApiKey: vi.fn(() => 'pk_test_testkey'),
    getMode: vi.fn(() => 'test'),
    setApiKey: vi.fn(),
    clearApiKey: vi.fn(),
    getDashboardStats: vi.fn(),
  },
}));

import { apiClient } from '../../api/client';

function renderDashboard() {
  return render(
    <MemoryRouter>
      <AuthProvider>
        <DashboardPage />
      </AuthProvider>
    </MemoryRouter>
  );
}

describe('DashboardPage', () => {
  beforeEach(() => {
    localStorageMock.clear();
    localStorageMock.setItem('payflow_api_key', 'pk_test_testkey');
    localStorageMock.setItem('payflow_mode', 'test');
    vi.mocked(apiClient.getDashboardStats).mockResolvedValue({
      totalPayments: 42,
      totalAmount: 1250.50,
      successRate: 95.5,
      pendingSettlements: 3,
    });
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('should show loading state initially', () => {
    // Make the API call hang to test loading state
    vi.mocked(apiClient.getDashboardStats).mockReturnValue(new Promise(() => {}));

    renderDashboard();

    // Look for the spinner element by its class pattern
    const spinner = document.querySelector('.animate-spin');
    expect(spinner).toBeInTheDocument();
  });

  it('should render dashboard heading after loading', async () => {
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText('Dashboard')).toBeInTheDocument();
    });
  });

  it('should display mode indicator', async () => {
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText('test mode')).toBeInTheDocument();
    });
  });

  it('should render stats cards', async () => {
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText('Total Payments')).toBeInTheDocument();
      expect(screen.getByText('42')).toBeInTheDocument();
      expect(screen.getByText('Total Amount')).toBeInTheDocument();
      expect(screen.getByText('£1250.50')).toBeInTheDocument();
      expect(screen.getByText('Success Rate')).toBeInTheDocument();
      expect(screen.getByText('95.5%')).toBeInTheDocument();
      expect(screen.getByText('Pending Settlements')).toBeInTheDocument();
      expect(screen.getByText('3')).toBeInTheDocument();
    });
  });

  it('should render quick actions', async () => {
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText('Quick Actions')).toBeInTheDocument();
      expect(screen.getByText('Create Payment')).toBeInTheDocument();
      expect(screen.getByText('View Payments')).toBeInTheDocument();
      expect(screen.getByText('Settlements')).toBeInTheDocument();
    });
  });

  it('should show error message on API failure', async () => {
    vi.mocked(apiClient.getDashboardStats).mockRejectedValue(new Error('Network error'));

    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText('Failed to load dashboard data')).toBeInTheDocument();
    });
  });

  it('should render Recent Activity section', async () => {
    renderDashboard();

    await waitFor(() => {
      expect(screen.getByText('Recent Activity')).toBeInTheDocument();
      expect(screen.getByText('No recent activity')).toBeInTheDocument();
    });
  });
});
