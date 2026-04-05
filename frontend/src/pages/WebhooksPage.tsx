import { useState, useEffect } from 'react';
import { apiClient, ApiError } from '../api/client';
import type { WebhookEndpoint, CreateWebhookEndpointRequest } from '../types';
import {
  Webhook,
  Plus,
  AlertCircle,
  CheckCircle,
  Loader2,
  Trash2,
  RefreshCw,
  Eye,
  EyeOff,
  Copy,
} from 'lucide-react';

const EVENT_TYPES = [
  'payment.created',
  'payment.authorised',
  'payment.captured',
  'payment.settled',
  'payment.failed',
  'payment.cancelled',
  'refund.created',
  'refund.succeeded',
  'refund.failed',
];

export function WebhooksPage() {
  const [endpoints, setEndpoints] = useState<WebhookEndpoint[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Create modal state
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [createUrl, setCreateUrl] = useState('');
  const [createSecret, setCreateSecret] = useState('');
  const [createEventTypes, setCreateEventTypes] = useState<string[]>([]);
  const [createUrlError, setCreateUrlError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [newSecret, setNewSecret] = useState<string | null>(null);
  const [showSecret, setShowSecret] = useState(false);
  const [copied, setCopied] = useState(false);

  // Rotate secret state
  const [rotatingId, setRotatingId] = useState<string | null>(null);
  const [rotatedSecret, setRotatedSecret] = useState<string | null>(null);

  useEffect(() => {
    loadEndpoints();
  }, []);

  const loadEndpoints = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await apiClient.getWebhookEndpoints();
      setEndpoints(data);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('Failed to load webhook endpoints');
      }
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreateUrlError(null);
    setCreating(true);

    // Validate HTTPS
    if (!createUrl.startsWith('https://')) {
      setCreateUrlError('URL must use HTTPS');
      setCreating(false);
      return;
    }

    try {
      const request: CreateWebhookEndpointRequest = {
        url: createUrl,
        secret: createSecret,
        eventTypes: createEventTypes,
      };

      const endpoint = await apiClient.createWebhookEndpoint(request);
      setNewSecret(createSecret);
      setEndpoints([endpoint, ...endpoints]);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('Failed to create webhook endpoint');
      }
    } finally {
      setCreating(false);
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await apiClient.deleteWebhookEndpoint(id);
      setEndpoints(endpoints.filter((e) => e.id !== id));
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      }
    }
  };

  const handleRotateSecret = async (id: string) => {
    const newSecretValue = 'whsec_' + Math.random().toString(36).substring(2, 30);
    setRotatingId(id);

    try {
      await apiClient.rotateWebhookSecret(id, { newSecret: newSecretValue });
      setRotatedSecret(newSecretValue);
      await loadEndpoints();
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      }
    } finally {
      setRotatingId(null);
    }
  };

  const handleCopySecret = async (secret: string) => {
    await navigator.clipboard.writeText(secret);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const toggleEventType = (type: string) => {
    setCreateEventTypes((prev) =>
      prev.includes(type) ? prev.filter((t) => t !== type) : [...prev, type]
    );
  };

  const handleCloseCreateModal = () => {
    setShowCreateModal(false);
    setCreateUrl('');
    setCreateSecret('');
    setCreateEventTypes([]);
    setCreateUrlError(null);
    setNewSecret(null);
    setShowSecret(false);
    setCopied(false);
  };

  const handleCloseRotateModal = () => {
    setRotatedSecret(null);
    setRotatingId(null);
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <Loader2 className="h-8 w-8 animate-spin text-indigo-600" />
      </div>
    );
  }

  return (
    <div>
      <div className="sm:flex sm:items-center sm:justify-between mb-8">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Webhooks</h1>
          <p className="mt-1 text-sm text-gray-500">
            Configure webhook endpoints to receive event notifications.
          </p>
        </div>
        <div className="mt-4 sm:mt-0">
          <button
            onClick={() => setShowCreateModal(true)}
            className="inline-flex items-center px-4 py-2 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500"
          >
            <Plus className="h-4 w-4 mr-2" />
            Add Endpoint
          </button>
        </div>
      </div>

      {error && (
        <div className="mb-4 p-4 bg-red-50 rounded-md flex items-center">
          <AlertCircle className="h-5 w-5 text-red-400 mr-2" />
          <span className="text-sm text-red-700">{error}</span>
        </div>
      )}

      {/* Endpoints List */}
      <div className="bg-white shadow overflow-hidden sm:rounded-md">
        <ul className="divide-y divide-gray-200">
          {endpoints.length === 0 ? (
            <li className="px-4 py-8 text-center text-gray-500">
              <Webhook className="h-12 w-12 mx-auto text-gray-300 mb-4" />
              <p>No webhook endpoints configured.</p>
              <p className="text-sm">Add an endpoint to receive events.</p>
            </li>
          ) : (
            endpoints.map((endpoint) => (
              <li key={endpoint.id}>
                <div className="px-4 py-4 sm:px-6">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center">
                      <div className="flex-shrink-0">
                        <Webhook
                          className={`h-8 w-8 ${
                            endpoint.status === 'active'
                              ? 'text-green-500'
                              : 'text-gray-400'
                          }`}
                        />
                      </div>
                      <div className="ml-4">
                        <div className="flex items-center">
                          <span className="text-sm font-medium text-gray-900">
                            {endpoint.url}
                          </span>
                          <span
                            className={`ml-2 px-2 inline-flex text-xs leading-5 font-semibold rounded-full ${
                              endpoint.status === 'active'
                                ? 'bg-green-100 text-green-800'
                                : 'bg-gray-100 text-gray-800'
                            }`}
                          >
                            {endpoint.status}
                          </span>
                        </div>
                        <div className="mt-1 text-sm text-gray-500">
                          Events: {endpoint.eventTypes.join(', ')}
                        </div>
                        <div className="mt-1 text-xs text-gray-400">
                          Created {new Date(endpoint.createdAt).toLocaleDateString()}
                          {endpoint.lastRotatedAt && (
                            <span className="ml-2">
                              • Secret rotated{' '}
                              {new Date(endpoint.lastRotatedAt).toLocaleDateString()}
                            </span>
                          )}
                        </div>
                      </div>
                    </div>
                    <div className="flex space-x-2">
                      <button
                        onClick={() => handleRotateSecret(endpoint.id)}
                        disabled={rotatingId === endpoint.id}
                        className="p-2 text-gray-400 hover:text-indigo-500"
                        title="Rotate secret"
                      >
                        {rotatingId === endpoint.id ? (
                          <Loader2 className="h-5 w-5 animate-spin" />
                        ) : (
                          <RefreshCw className="h-5 w-5" />
                        )}
                      </button>
                      <button
                        onClick={() => handleDelete(endpoint.id)}
                        className="p-2 text-gray-400 hover:text-red-500"
                        title="Delete endpoint"
                      >
                        <Trash2 className="h-5 w-5" />
                      </button>
                    </div>
                  </div>
                </div>
              </li>
            ))
          )}
        </ul>
      </div>

      {/* Create Endpoint Modal */}
      {showCreateModal && (
        <div className="fixed z-10 inset-0 overflow-y-auto">
          <div className="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
            <div
              className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity"
              onClick={handleCloseCreateModal}
            />

            <div className="inline-block align-bottom bg-white rounded-lg px-4 pt-5 pb-4 text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-lg sm:w-full sm:p-6">
              {!newSecret ? (
                <>
                  <div>
                    <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-indigo-100">
                      <Webhook className="h-6 w-6 text-indigo-600" />
                    </div>
                    <div className="mt-3 text-center sm:mt-5">
                      <h3 className="text-lg leading-6 font-medium text-gray-900">
                        Add Webhook Endpoint
                      </h3>
                    </div>
                  </div>

                  <form onSubmit={handleCreate} className="mt-4">
                    <div className="space-y-4">
                      <div>
                        <label
                          htmlFor="url"
                          className="block text-sm font-medium text-gray-700"
                        >
                          Endpoint URL
                        </label>
                        <input
                          type="url"
                          id="url"
                          required
                          placeholder="https://your-domain.com/webhooks"
                          className={`mt-1 block w-full border ${
                            createUrlError ? 'border-red-500' : 'border-gray-300'
                          } rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm`}
                          value={createUrl}
                          onChange={(e) => setCreateUrl(e.target.value)}
                          disabled={creating}
                        />
                        {createUrlError && (
                          <p className="mt-1 text-sm text-red-600">
                            {createUrlError}
                          </p>
                        )}
                      </div>

                      <div>
                        <label
                          htmlFor="secret"
                          className="block text-sm font-medium text-gray-700"
                        >
                          Signing Secret
                        </label>
                        <input
                          type="text"
                          id="secret"
                          required
                          minLength={16}
                          placeholder="At least 16 characters"
                          className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm"
                          value={createSecret}
                          onChange={(e) => setCreateSecret(e.target.value)}
                          disabled={creating}
                        />
                      </div>

                      <div>
                        <label className="block text-sm font-medium text-gray-700 mb-2">
                          Event Types
                        </label>
                        <div className="grid grid-cols-2 gap-2">
                          {EVENT_TYPES.map((type) => (
                            <label
                              key={type}
                              className="flex items-center text-sm"
                            >
                              <input
                                type="checkbox"
                                className="h-4 w-4 text-indigo-600 focus:ring-indigo-500 border-gray-300 rounded"
                                checked={createEventTypes.includes(type)}
                                onChange={() => toggleEventType(type)}
                                disabled={creating}
                              />
                              <span className="ml-2 text-gray-700">{type}</span>
                            </label>
                          ))}
                        </div>
                      </div>
                    </div>

                    <div className="mt-5 sm:mt-6 sm:grid sm:grid-cols-2 sm:gap-3 sm:grid-flow-row-dense">
                      <button
                        type="submit"
                        disabled={creating || createEventTypes.length === 0}
                        className="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-indigo-600 text-base font-medium text-white hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:col-start-2 sm:text-sm disabled:opacity-50"
                      >
                        {creating ? (
                          <>
                            <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                            Creating...
                          </>
                        ) : (
                          'Create Endpoint'
                        )}
                      </button>
                      <button
                        type="button"
                        className="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:mt-0 sm:col-start-1 sm:text-sm"
                        onClick={handleCloseCreateModal}
                        disabled={creating}
                      >
                        Cancel
                      </button>
                    </div>
                  </form>
                </>
              ) : (
                <>
                  <div>
                    <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-green-100">
                      <CheckCircle className="h-6 w-6 text-green-600" />
                    </div>
                    <div className="mt-3 text-center sm:mt-5">
                      <h3 className="text-lg leading-6 font-medium text-gray-900">
                        Endpoint Created
                      </h3>
                      <div className="mt-4">
                        <div className="bg-yellow-50 border border-yellow-200 rounded-md p-4 mb-4">
                          <div className="flex">
                            <AlertCircle className="h-5 w-5 text-yellow-400" />
                            <div className="ml-3">
                              <p className="text-sm text-yellow-700">
                                This is the only time your signing secret will be
                                shown. Please copy it now.
                              </p>
                            </div>
                          </div>
                        </div>
                        <div className="bg-gray-100 rounded-md p-3 flex items-center justify-between">
                          <code className="text-sm text-gray-800 break-all">
                            {showSecret
                              ? newSecret
                              : '••••••••••••••••••••••••'}
                          </code>
                          <div className="flex space-x-2 ml-2">
                            <button
                              onClick={() => setShowSecret(!showSecret)}
                              className="p-1 text-gray-500 hover:text-gray-700"
                            >
                              {showSecret ? (
                                <EyeOff className="h-4 w-4" />
                              ) : (
                                <Eye className="h-4 w-4" />
                              )}
                            </button>
                            <button
                              onClick={() => handleCopySecret(newSecret)}
                              className="p-1 text-gray-500 hover:text-gray-700"
                            >
                              {copied ? (
                                <CheckCircle className="h-4 w-4 text-green-500" />
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
                      onClick={handleCloseCreateModal}
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

      {/* Rotate Secret Modal */}
      {rotatedSecret && (
        <div className="fixed z-10 inset-0 overflow-y-auto">
          <div className="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
            <div
              className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity"
              onClick={handleCloseRotateModal}
            />

            <div className="inline-block align-bottom bg-white rounded-lg px-4 pt-5 pb-4 text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-lg sm:w-full sm:p-6">
              <div>
                <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-green-100">
                  <CheckCircle className="h-6 w-6 text-green-600" />
                </div>
                <div className="mt-3 text-center sm:mt-5">
                  <h3 className="text-lg leading-6 font-medium text-gray-900">
                    Secret Rotated
                  </h3>
                  <div className="mt-4">
                    <div className="bg-yellow-50 border border-yellow-200 rounded-md p-4 mb-4">
                      <div className="flex">
                        <AlertCircle className="h-5 w-5 text-yellow-400" />
                        <div className="ml-3">
                          <p className="text-sm text-yellow-700">
                            This is the only time your new signing secret will be
                            shown. Please copy it now.
                          </p>
                        </div>
                      </div>
                    </div>
                    <div className="bg-gray-100 rounded-md p-3 flex items-center justify-between">
                      <code className="text-sm text-gray-800 break-all">
                        {showSecret
                          ? rotatedSecret
                          : '••••••••••••••••••••••••'}
                      </code>
                      <div className="flex space-x-2 ml-2">
                        <button
                          onClick={() => setShowSecret(!showSecret)}
                          className="p-1 text-gray-500 hover:text-gray-700"
                        >
                          {showSecret ? (
                            <EyeOff className="h-4 w-4" />
                          ) : (
                            <Eye className="h-4 w-4" />
                          )}
                        </button>
                        <button
                          onClick={() => handleCopySecret(rotatedSecret)}
                          className="p-1 text-gray-500 hover:text-gray-700"
                        >
                          {copied ? (
                            <CheckCircle className="h-4 w-4 text-green-500" />
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
                  onClick={handleCloseRotateModal}
                >
                  Done
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}