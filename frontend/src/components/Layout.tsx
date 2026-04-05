import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import {
  CreditCard,
  Key,
  Webhook,
  Receipt,
  Settings,
  LogOut,
  ToggleLeft,
  ToggleRight,
  AlertTriangle,
} from 'lucide-react';

interface LayoutProps {
  children: React.ReactNode;
}

export function Layout({ children }: LayoutProps) {
  const { isAuthenticated, mode, logout, toggleMode, isSuspended } = useAuth();
  const location = useLocation();

  const navItems = [
    { path: '/', label: 'Dashboard', icon: CreditCard },
    { path: '/api-keys', label: 'API Keys', icon: Key },
    { path: '/payments', label: 'Payments', icon: Receipt },
    { path: '/webhooks', label: 'Webhooks', icon: Webhook },
    { path: '/settlements', label: 'Settlements', icon: Settings },
  ];

  if (!isAuthenticated) {
    return <>{children}</>;
  }

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Navigation */}
      <nav className="bg-white shadow-sm border-b border-gray-200">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between h-16">
            <div className="flex">
              {/* Logo */}
              <div className="flex-shrink-0 flex items-center">
                <Link to="/" className="text-xl font-bold text-indigo-600">
                  PayFlow
                </Link>
              </div>

              {/* Nav Links */}
              <div className="hidden sm:ml-8 sm:flex sm:space-x-4">
                {navItems.map((item) => {
                  const Icon = item.icon;
                  const isActive = location.pathname === item.path;
                  return (
                    <Link
                      key={item.path}
                      to={item.path}
                      className={`inline-flex items-center px-3 py-2 text-sm font-medium rounded-md ${
                        isActive
                          ? 'text-indigo-600 bg-indigo-50'
                          : 'text-gray-500 hover:text-gray-700 hover:bg-gray-50'
                      }`}
                    >
                      <Icon className="w-4 h-4 mr-2" />
                      {item.label}
                    </Link>
                  );
                })}
              </div>
            </div>

            {/* Right side */}
            <div className="flex items-center space-x-4">
              {/* Mode Toggle */}
              <button
                onClick={toggleMode}
                className={`inline-flex items-center px-3 py-1.5 rounded-full text-sm font-medium ${
                  mode === 'live'
                    ? 'bg-green-100 text-green-800'
                    : 'bg-yellow-100 text-yellow-800'
                }`}
              >
                {mode === 'live' ? (
                  <ToggleRight className="w-4 h-4 mr-1" />
                ) : (
                  <ToggleLeft className="w-4 h-4 mr-1" />
                )}
                {mode === 'live' ? 'Live Mode' : 'Test Mode'}
              </button>

              {/* Suspension Warning */}
              {isSuspended && (
                <div className="flex items-center text-yellow-600">
                  <AlertTriangle className="w-4 h-4 mr-1" />
                  <span className="text-sm">Suspended</span>
                </div>
              )}

              {/* Logout */}
              <button
                onClick={logout}
                className="inline-flex items-center px-3 py-2 text-sm font-medium text-gray-500 hover:text-gray-700"
              >
                <LogOut className="w-4 h-4 mr-1" />
                Logout
              </button>
            </div>
          </div>
        </div>
      </nav>

      {/* Main Content */}
      <main className="max-w-7xl mx-auto py-6 px-4 sm:px-6 lg:px-8">
        {children}
      </main>
    </div>
  );
}