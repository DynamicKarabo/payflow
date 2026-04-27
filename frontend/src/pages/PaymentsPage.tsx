import { useState } from 'react';
import { Link } from 'react-router-dom';
import { apiClient, ApiError } from '../api/client';
import { useAuth } from '../contexts/AuthContext';
import type { Payment, CreatePaymentRequest } from '../types';
import {
  CreditCard,
  Plus,
  AlertCircle,
  CheckCircle,
  Clock,
  XCircle,
  Loader2,
} from 'lucide-react';

export function PaymentsPage() {
  const { isSuspended } = useAuth();
  const [payments, setPayments] = useState<Payment[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);

  // Form state
  const [amount, setAmount] = useState('');
  const [currency, setCurrency] = useState('GBP');
  const [customerId, setCustomerId] = useState('');
  const [cardToken, setCardToken] = useState('');
  const [autoCapture, setAutoCapture] = useState(false);
  const [formErrors, setFormErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);
  const [paymentStatus, setPaymentStatus] = useState<string | null>(null);

  const handleCreatePayment = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormErrors({});
    setError(null);
    setSubmitting(true);
    setPaymentStatus('created');

    try {
      const request: CreatePaymentRequest = {
        amount: Math.round(parseFloat(amount) * 100), // Convert to cents
        currency,
        customerId,
        paymentMethod: {
          type: 'card',
          token: cardToken || undefined,
        },
        autoCapture,
      };

      const payment = await apiClient.createPayment(request);
      setPaymentStatus(payment.status);
      setPayments([payment, ...payments]);
      
      // Close modal after successful creation
      setTimeout(() => {
        setShowCreateModal(false);
        resetForm();
      }, 1500);
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 422 && err.problemDetails?.errors) {
          const newFormErrors: Record<string, string> = {};
          for (const [key, value] of Object.entries(err.problemDetails.errors)) {
            newFormErrors[key] = Array.isArray(value) ? value[0] : value;
          }
          setFormErrors(newFormErrors);
        } else if (err.status === 409) {
          setError('Payment is already being processed. Please wait...');
          setPaymentStatus('processing');
        } else {
          setError(err.message);
        }
      } else {
        setError('An unexpected error occurred');
      }
    } finally {
      setSubmitting(false);
    }
  };

  const resetForm = () => {
    setAmount('');
    setCurrency('GBP');
    setCustomerId('');
    setCardToken('');
    setAutoCapture(false);
    setFormErrors({});
    setError(null);
    setPaymentStatus(null);
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'captured':
      case 'settled':
        return <CheckCircle className="h-5 w-5 text-green-500" />;
      case 'authorised':
        return <Clock className="h-5 w-5 text-yellow-500" />;
      case 'failed':
      case 'cancelled':
        return <XCircle className="h-5 w-5 text-red-500" />;
      default:
        return <Clock className="h-5 w-5 text-gray-500" />;
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'captured':
      case 'settled':
        return 'bg-green-100 text-green-800';
      case 'authorised':
        return 'bg-yellow-100 text-yellow-800';
      case 'failed':
      case 'cancelled':
        return 'bg-red-100 text-red-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  return (
    <div>
      <div className="sm:flex sm:items-center sm:justify-between mb-8">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Payments</h1>
          <p className="mt-1 text-sm text-gray-500">
            View and manage all payment transactions.
          </p>
        </div>
        <div className="mt-4 sm:mt-0">
          <button
            onClick={() => setShowCreateModal(true)}
            disabled={isSuspended}
            className="inline-flex items-center px-4 py-2 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Plus className="h-4 w-4 mr-2" />
            Create Payment
          </button>
        </div>
      </div>

      {error && !showCreateModal && (
        <div className="mb-4 p-4 bg-red-50 rounded-md flex items-center">
          <AlertCircle className="h-5 w-5 text-red-400 mr-2" />
          <span className="text-sm text-red-700">{error}</span>
        </div>
      )}

      {/* Payments List */}
      <div className="bg-white shadow overflow-hidden sm:rounded-md">
        <ul className="divide-y divide-gray-200">
          {payments.length === 0 ? (
            <li className="px-4 py-8 text-center text-gray-500">
              <CreditCard className="h-12 w-12 mx-auto text-gray-300 mb-4" />
              <p>No payments yet.</p>
              <p className="text-sm">Create your first payment to get started.</p>
            </li>
          ) : (
            payments.map((payment) => (
              <li key={payment.id}>
                <Link
                  to={`/payments/${payment.id}`}
                  className="block hover:bg-gray-50"
                >
                  <div className="px-4 py-4 flex items-center sm:px-6">
                    <div className="min-w-0 flex-1 sm:flex sm:items-center sm:justify-between">
                      <div className="flex items-center">
                        <div className="flex-shrink-0">
                          {getStatusIcon(payment.status)}
                        </div>
                        <div className="ml-4">
                          <div className="flex items-center">
                            <span className="text-sm font-medium text-indigo-600 truncate">
                              {payment.id}
                            </span>
                            <span
                              className={`ml-2 px-2 inline-flex text-xs leading-5 font-semibold rounded-full ${getStatusColor(
                                payment.status
                              )}`}
                            >
                              {payment.status}
                            </span>
                          </div>
                          <div className="mt-1 text-sm text-gray-500">
                            {new Date(payment.createdAt).toLocaleString()}
                          </div>
                        </div>
                      </div>
                      <div className="mt-4 flex-shrink-0 sm:mt-0 sm:ml-5">
                        <div className="flex items-center">
                          <span className="text-lg font-medium text-gray-900">
                            {(payment.amount / 100).toFixed(2)} {payment.currency}
                          </span>
                          {payment.fraudScore !== undefined && payment.fraudScore > 0.8 && (
                            <span className="ml-2 px-2 inline-flex text-xs leading-5 font-semibold rounded-full bg-red-100 text-red-800">
                              High Risk
                            </span>
                          )}
                          {payment.fraudScore !== undefined && payment.fraudScore > 0.5 && payment.fraudScore <= 0.8 && (
                            <span className="ml-2 px-2 inline-flex text-xs leading-5 font-semibold rounded-full bg-yellow-100 text-yellow-800">
                              Medium Risk
                            </span>
                          )}
                          {payment.fraudScore !== undefined && payment.fraudScore <= 0.5 && (
                            <span className="ml-2 px-2 inline-flex text-xs leading-5 font-semibold rounded-full bg-green-100 text-green-800">
                              Low Risk
                            </span>
                          )}
                        </div>
                      </div>
                    </div>
                  </div>
                </Link>
              </li>
            ))
          )}
        </ul>
      </div>

      {/* Create Payment Modal */}
      {showCreateModal && (
        <div className="fixed z-10 inset-0 overflow-y-auto">
          <div className="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
            <div
              className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity"
              onClick={() => {
                setShowCreateModal(false);
                resetForm();
              }}
            />

            <div className="inline-block align-bottom bg-white rounded-lg px-4 pt-5 pb-4 text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-lg sm:w-full sm:p-6">
              <div>
                <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-indigo-100">
                  <CreditCard className="h-6 w-6 text-indigo-600" />
                </div>
                <div className="mt-3 text-center sm:mt-5">
                  <h3 className="text-lg leading-6 font-medium text-gray-900">
                    Create Payment
                  </h3>
                </div>
              </div>

              {/* Payment Status Indicator */}
              {paymentStatus && (
                <div className="mt-4 flex items-center justify-center space-x-2">
                  <div
                    className={`flex items-center px-3 py-1 rounded-full text-sm ${
                      paymentStatus === 'captured'
                        ? 'bg-green-100 text-green-800'
                        : paymentStatus === 'authorised'
                        ? 'bg-yellow-100 text-yellow-800'
                        : 'bg-gray-100 text-gray-800'
                    }`}
                  >
                    {paymentStatus === 'processing' && (
                      <Loader2 className="h-4 w-4 mr-1 animate-spin" />
                    )}
                    {paymentStatus === 'created' && 'Created'}
                    {paymentStatus === 'authorised' && 'Authorised'}
                    {paymentStatus === 'captured' && 'Captured'}
                    {paymentStatus === 'processing' && 'Processing...'}
                  </div>
                </div>
              )}

              <form onSubmit={handleCreatePayment} className="mt-4">
                {error && (
                  <div className="mb-4 p-3 bg-red-50 rounded-md flex items-center">
                    <AlertCircle className="h-4 w-4 text-red-400 mr-2" />
                    <span className="text-sm text-red-700">{error}</span>
                  </div>
                )}

                <div className="space-y-4">
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label
                        htmlFor="amount"
                        className="block text-sm font-medium text-gray-700"
                      >
                        Amount
                      </label>
                      <input
                        type="number"
                        id="amount"
                        step="0.01"
                        min="0.01"
                        required
                        className={`mt-1 block w-full border ${
                          formErrors.amount ? 'border-red-500' : 'border-gray-300'
                        } rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm`}
                        value={amount}
                        onChange={(e) => setAmount(e.target.value)}
                        disabled={submitting}
                      />
                      {formErrors.amount && (
                        <p className="mt-1 text-sm text-red-600">
                          {formErrors.amount}
                        </p>
                      )}
                    </div>
                    <div>
                      <label
                        htmlFor="currency"
                        className="block text-sm font-medium text-gray-700"
                      >
                        Currency
                      </label>
                      <select
                        id="currency"
                        required
                        className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm"
                        value={currency}
                        onChange={(e) => setCurrency(e.target.value)}
                        disabled={submitting}
                      >
                        <option value="GBP">GBP</option>
                        <option value="USD">USD</option>
                        <option value="EUR">EUR</option>
                      </select>
                    </div>
                  </div>

                  <div>
                    <label
                      htmlFor="customerId"
                      className="block text-sm font-medium text-gray-700"
                    >
                      Customer ID
                    </label>
                    <input
                      type="text"
                      id="customerId"
                      required
                      className={`mt-1 block w-full border ${
                        formErrors.customerId
                          ? 'border-red-500'
                          : 'border-gray-300'
                      } rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm`}
                      value={customerId}
                      onChange={(e) => setCustomerId(e.target.value)}
                      disabled={submitting}
                    />
                    {formErrors.customerId && (
                      <p className="mt-1 text-sm text-red-600">
                        {formErrors.customerId}
                      </p>
                    )}
                  </div>

                  <div>
                    <label
                      htmlFor="cardToken"
                      className="block text-sm font-medium text-gray-700"
                    >
                      Card Token (optional)
                    </label>
                    <input
                      type="text"
                      id="cardToken"
                      className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm"
                      value={cardToken}
                      onChange={(e) => setCardToken(e.target.value)}
                      placeholder="tok_xxx"
                      disabled={submitting}
                    />
                  </div>

                  <div className="flex items-center">
                    <input
                      id="autoCapture"
                      type="checkbox"
                      className="h-4 w-4 text-indigo-600 focus:ring-indigo-500 border-gray-300 rounded"
                      checked={autoCapture}
                      onChange={(e) => setAutoCapture(e.target.checked)}
                      disabled={submitting}
                    />
                    <label
                      htmlFor="autoCapture"
                      className="ml-2 block text-sm text-gray-900"
                    >
                      Auto-capture payment
                    </label>
                  </div>
                </div>

                <div className="mt-5 sm:mt-6 sm:grid sm:grid-cols-2 sm:gap-3 sm:grid-flow-row-dense">
                  <button
                    type="submit"
                    disabled={submitting}
                    className="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-indigo-600 text-base font-medium text-white hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:col-start-2 sm:text-sm disabled:opacity-50"
                  >
                    {submitting ? (
                      <>
                        <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                        Processing...
                      </>
                    ) : (
                      'Create Payment'
                    )}
                  </button>
                  <button
                    type="button"
                    className="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:mt-0 sm:col-start-1 sm:text-sm"
                    onClick={() => {
                      setShowCreateModal(false);
                      resetForm();
                    }}
                    disabled={submitting}
                  >
                    Cancel
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}