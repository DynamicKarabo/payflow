import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import { AuthProvider, useAuth } from '../AuthContext';

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

// Mock crypto for apiClient
Object.defineProperty(globalThis, 'crypto', {
  value: {
    randomUUID: () => '12345678-1234-1234-1234-123456789abc',
  },
});

// Test component to expose auth context
function TestConsumer() {
  const { isAuthenticated, apiKey, mode, login, logout, toggleMode } = useAuth();
  return (
    <div>
      <span data-testid="authenticated">{isAuthenticated.toString()}</span>
      <span data-testid="apikey">{apiKey ?? 'null'}</span>
      <span data-testid="mode">{mode}</span>
      <button onClick={() => login('pk_test_testkey123')}>Login</button>
      <button onClick={() => login('pk_live_livekey456')}>Login Live</button>
      <button onClick={logout}>Logout</button>
      <button onClick={toggleMode}>Toggle Mode</button>
    </div>
  );
}

describe('AuthContext', () => {
  beforeEach(() => {
    localStorageMock.clear();
  });

  it('should render children within AuthProvider', () => {
    render(
      <AuthProvider>
        <div data-testid="child">Hello</div>
      </AuthProvider>
    );
    expect(screen.getByTestId('child')).toBeInTheDocument();
  });

  it('should throw error when useAuth is used outside provider', () => {
    // Suppress console.error for this test
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {});
    expect(() => render(<TestConsumer />)).toThrow('useAuth must be used within an AuthProvider');
    spy.mockRestore();
  });

  it('should start unauthenticated', () => {
    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>
    );
    expect(screen.getByTestId('authenticated')).toHaveTextContent('false');
    expect(screen.getByTestId('apikey')).toHaveTextContent('null');
  });

  it('should login and set API key', () => {
    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>
    );

    act(() => {
      screen.getByText('Login').click();
    });

    expect(screen.getByTestId('authenticated')).toHaveTextContent('true');
    expect(screen.getByTestId('apikey')).toHaveTextContent('pk_test_testkey123');
    expect(screen.getByTestId('mode')).toHaveTextContent('test');
  });

  it('should logout and clear API key', () => {
    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>
    );

    act(() => {
      screen.getByText('Login').click();
    });

    act(() => {
      screen.getByText('Logout').click();
    });

    expect(screen.getByTestId('authenticated')).toHaveTextContent('false');
    expect(screen.getByTestId('apikey')).toHaveTextContent('null');
  });

  it('should toggle mode from test to live', () => {
    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>
    );

    act(() => {
      screen.getByText('Login').click();
    });

    expect(screen.getByTestId('mode')).toHaveTextContent('test');

    act(() => {
      screen.getByText('Toggle Mode').click();
    });

    expect(screen.getByTestId('mode')).toHaveTextContent('live');
    expect(screen.getByTestId('apikey')).toHaveTextContent('pk_live_');
  });

  it('should toggle mode from live to test', () => {
    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>
    );

    act(() => {
      screen.getByText('Login Live').click();
    });

    expect(screen.getByTestId('mode')).toHaveTextContent('live');

    act(() => {
      screen.getByText('Toggle Mode').click();
    });

    expect(screen.getByTestId('mode')).toHaveTextContent('test');
    expect(screen.getByTestId('apikey')).toHaveTextContent('pk_test_');
  });
});
