import { useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import {
  Key,
  Plus,
  Copy,
  Check,
  AlertTriangle,
  Eye,
  EyeOff,
  Trash2,
} from 'lucide-react';

interface ApiKey {
  id: string;
  keyPrefix: string;
  mode: 'test' | 'live';
  status: 'active' | 'revoked';
  createdAt: string;
  fullKey?: string;
}

export function ApiKeysPage() {
  const { mode, isSuspended } = useAuth();
  const [apiKeys, setApiKeys] = useState<ApiKey[]>([
    {
      id: '1',
      keyPrefix: 'pk_test_abc123',
      mode: 'test',
      status: 'active',
      createdAt: new Date().toISOString(),
    },
  ]);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [newKey, setNewKey] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [showKey, setShowKey] = useState<Record<string, boolean>>({});

  const handleCreateKey = () => {
    const prefix = mode === 'live' ? 'pk_live_' : 'pk_test_';
    const randomPart = Math.random().toString(36).substring(2, 15) + Math.random().toString(36).substring(2, 15);
    const fullKey = prefix + randomPart;
    
    const newApiKey: ApiKey = {
      id: Date.now().toString(),
      keyPrefix: fullKey.substring(0, 12),
      mode,
      status: 'active',
      createdAt: new Date().toISOString(),
      fullKey,
    };

    setApiKeys([...apiKeys, newApiKey]);
    setNewKey(fullKey);
  };

  const handleCopyKey = async () => {
    if (newKey) {
      await navigator.clipboard.writeText(newKey);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  const handleCloseModal = () => {
    setShowCreateModal(false);
    setNewKey(null);
    setCopied(false);
  };

  const revokeKey = (id: string) => {
    setApiKeys(
      apiKeys.map((key) =>
        key.id === id ? { ...key, status: 'revoked' as const } : key
      )
    );
  };

  const filteredKeys = apiKeys.filter((key) => key.mode === mode);

  return (
    <div>
      <div className="sm:flex sm:items-center sm:justify-between mb-8">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">API Keys</h1>
          <p className="mt-1 text-sm text-gray-500">
            Manage your {mode} API keys for authenticating requests.
          </p>
        </div>
        <div className="mt-4 sm:mt-0">
          <button
            onClick={() => setShowCreateModal(true)}
            disabled={isSuspended}
            className="inline-flex items-center px-4 py-2 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Plus className="h-4 w-4 mr-2" />
            Generate New Key
          </button>
        </div>
      </div>

      {isSuspended && (
        <div className="mb-4 p-4 bg-yellow-50 rounded-md flex items-center">
          <AlertTriangle className="h-5 w-5 text-yellow-400 mr-2" />
          <span className="text-sm text-yellow-700">
            Your account is suspended. You cannot generate new API keys.
          </span>
        </div>
      )}

      {/* API Keys List */}
      <div className="bg-white shadow overflow-hidden sm:rounded-md">
        <ul className="divide-y divide-gray-200">
          {filteredKeys.length === 0 ? (
            <li className="px-4 py-8 text-center text-gray-500">
              <Key className="h-12 w-12 mx-auto text-gray-300 mb-4" />
              <p>No {mode} API keys yet.</p>
              <p className="text-sm">Generate your first key to get started.</p>
            </li>
          ) : (
            filteredKeys.map((apiKey) => (
              <li key={apiKey.id}>
                <div className="px-4 py-4 flex items-center sm:px-6">
                  <div className="min-w-0 flex-1 sm:flex sm:items-center sm:justify-between">
                    <div className="flex items-center">
                      <div className="flex-shrink-0">
                        <Key
                          className={`h-8 w-8 ${
                            apiKey.mode === 'live'
                              ? 'text-green-500'
                              : 'text-yellow-500'
                          }`}
                        />
                      </div>
                      <div className="ml-4">
                        <div className="flex items-center">
                          <span className="text-sm font-medium text-gray-900">
                            {apiKey.keyPrefix}...
                          </span>
                          <span
                            className={`ml-2 px-2 inline-flex text-xs leading-5 font-semibold rounded-full ${
                              apiKey.status === 'active'
                                ? 'bg-green-100 text-green-800'
                                : 'bg-red-100 text-red-800'
                            }`}
                          >
                            {apiKey.status}
                          </span>
                        </div>
                        <div className="mt-1 text-sm text-gray-500">
                          Created {new Date(apiKey.createdAt).toLocaleDateString()}
                        </div>
                      </div>
                    </div>
                  </div>
                  <div className="ml-5 flex-shrink-0 flex space-x-2">
                    {apiKey.status === 'active' && (
                      <button
                        onClick={() => revokeKey(apiKey.id)}
                        className="p-2 text-gray-400 hover:text-red-500"
                        title="Revoke key"
                      >
                        <Trash2 className="h-5 w-5" />
                      </button>
                    )}
                  </div>
                </div>
              </li>
            ))
          )}
        </ul>
      </div>

      {/* Create Key Modal */}
      {showCreateModal && (
        <div className="fixed z-10 inset-0 overflow-y-auto">
          <div className="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
            <div
              className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity"
              onClick={handleCloseModal}
            />

            <div className="inline-block align-bottom bg-white rounded-lg px-4 pt-5 pb-4 text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-lg sm:w-full sm:p-6">
              {!newKey ? (
                <>
                  <div>
                    <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-indigo-100">
                      <Key className="h-6 w-6 text-indigo-600" />
                    </div>
                    <div className="mt-3 text-center sm:mt-5">
                      <h3 className="text-lg leading-6 font-medium text-gray-900">
                        Generate New API Key
                      </h3>
                      <div className="mt-2">
                        <p className="text-sm text-gray-500">
                          This will create a new {mode} API key. You'll only see
                          the full key once, so make sure to copy it.
                        </p>
                      </div>
                    </div>
                  </div>
                  <div className="mt-5 sm:mt-6 sm:grid sm:grid-cols-2 sm:gap-3 sm:grid-flow-row-dense">
                    <button
                      type="button"
                      className="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-indigo-600 text-base font-medium text-white hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:col-start-2 sm:text-sm"
                      onClick={handleCreateKey}
                    >
                      Generate
                    </button>
                    <button
                      type="button"
                      className="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:mt-0 sm:col-start-1 sm:text-sm"
                      onClick={handleCloseModal}
                    >
                      Cancel
                    </button>
                  </div>
                </>
              ) : (
                <>
                  <div>
                    <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-green-100">
                      <Check className="h-6 w-6 text-green-600" />
                    </div>
                    <div className="mt-3 text-center sm:mt-5">
                      <h3 className="text-lg leading-6 font-medium text-gray-900">
                        API Key Generated
                      </h3>
                      <div className="mt-4">
                        <div className="bg-yellow-50 border border-yellow-200 rounded-md p-4 mb-4">
                          <div className="flex">
                            <AlertTriangle className="h-5 w-5 text-yellow-400" />
                            <div className="ml-3">
                              <p className="text-sm text-yellow-700">
                                Please copy this key now. For security reasons, it
                                will never be shown again.
                              </p>
                            </div>
                          </div>
                        </div>
                        <div className="bg-gray-100 rounded-md p-3 flex items-center justify-between">
                          <code className="text-sm text-gray-800 break-all">
                            {showKey['new'] ? newKey : '••••••••••••••••••••••••'}
                          </code>
                          <div className="flex space-x-2 ml-2">
                            <button
                              onClick={() =>
                                setShowKey((prev) => ({
                                  ...prev,
                                  new: !prev['new'],
                                }))
                              }
                              className="p-1 text-gray-500 hover:text-gray-700"
                            >
                              {showKey['new'] ? (
                                <EyeOff className="h-4 w-4" />
                              ) : (
                                <Eye className="h-4 w-4" />
                              )}
                            </button>
                            <button
                              onClick={handleCopyKey}
                              className="p-1 text-gray-500 hover:text-gray-700"
                            >
                              {copied ? (
                                <Check className="h-4 w-4 text-green-500" />
                              ) : (
                                <Copy className="h-4 w-4" />
                              )}
                            </button>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                  <div className="mt-5 sm:mt-6">
                    <button
                      type="button"
                      className="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-indigo-600 text-base font-medium text-white hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:text-sm"
                      onClick={handleCloseModal}
                    >
                      Done
                    </button>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}