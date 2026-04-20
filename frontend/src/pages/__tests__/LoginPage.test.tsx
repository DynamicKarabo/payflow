import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { LoginPage } from '../LoginPage';
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

function renderLoginPage() {
  return render(
    <MemoryRouter>
      <AuthProvider>
        <LoginPage />
      </AuthProvider>
    </MemoryRouter>
  );
}

describe('LoginPage', () => {
  beforeEach(() => {
    localStorageMock.clear();
  });

  it('should render PayFlow heading', () => {
    renderLoginPage();
    expect(screen.getByText('PayFlow')).toBeInTheDocument();
  });

  it('should render sign in subtitle', () => {
    renderLoginPage();
    expect(screen.getByText('Sign in with your API key')).toBeInTheDocument();
  });

  it('should render API key input field', () => {
    renderLoginPage();
    expect(screen.getByPlaceholderText('pk_test_xxx or pk_live_xxx')).toBeInTheDocument();
  });

  it('should render sign in button', () => {
    renderLoginPage();
    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument();
  });

  it('should show error for whitespace-only input', async () => {
    const user = userEvent.setup();
    renderLoginPage();

    await user.type(screen.getByPlaceholderText('pk_test_xxx or pk_live_xxx'), '   ');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    expect(screen.getByText('Please enter an API key')).toBeInTheDocument();
  });

  it('should show error for invalid API key format', async () => {
    const user = userEvent.setup();
    renderLoginPage();

    await user.type(screen.getByPlaceholderText('pk_test_xxx or pk_live_xxx'), 'invalid_key');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    expect(screen.getByText('API key must start with pk_test_ or pk_live_')).toBeInTheDocument();
  });

  it('should display API key format hints', () => {
    renderLoginPage();
    expect(screen.getByText('pk_test_')).toBeInTheDocument();
    expect(screen.getByText('pk_live_')).toBeInTheDocument();
  });
});
