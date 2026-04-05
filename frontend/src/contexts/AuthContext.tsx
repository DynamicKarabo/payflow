import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';
import { apiClient } from '../api/client';

interface AuthContextType {
  isAuthenticated: boolean;
  apiKey: string | null;
  mode: 'test' | 'live';
  login: (apiKey: string) => void;
  logout: () => void;
  toggleMode: () => void;
  isSuspended: boolean;
  setSuspended: (suspended: boolean) => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [apiKey, setApiKey] = useState<string | null>(apiClient.getApiKey());
  const [mode, setMode] = useState<'test' | 'live'>(apiClient.getMode());
  const [isSuspended, setIsSuspended] = useState(false);

  useEffect(() => {
    const savedKey = apiClient.getApiKey();
    if (savedKey) {
      setApiKey(savedKey);
      setMode(apiClient.getMode());
    }
  }, []);

  const login = (key: string) => {
    apiClient.setApiKey(key);
    setApiKey(key);
    setMode(apiClient.getMode());
    setIsSuspended(false);
  };

  const logout = () => {
    apiClient.clearApiKey();
    setApiKey(null);
    setMode('test');
    setIsSuspended(false);
  };

  const toggleMode = () => {
    if (!apiKey) return;
    
    const prefix = apiKey.startsWith('pk_live_') ? 'pk_live_' : 'pk_test_';
    const suffix = apiKey.slice(prefix.length);
    const newPrefix = mode === 'test' ? 'pk_live_' : 'pk_test_';
    const newKey = newPrefix + suffix;
    
    login(newKey);
  };

  return (
    <AuthContext.Provider
      value={{
        isAuthenticated: !!apiKey,
        apiKey,
        mode,
        login,
        logout,
        toggleMode,
        isSuspended,
        setSuspended: setIsSuspended,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}