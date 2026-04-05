import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { apiClient, ApiError } from '../api/client';
import type { Payment } from '../types';
import {
  ArrowLeft,
  CheckCircle,
  Clock,
  XCircle,
  AlertCircle,
  Loader2,
  RefreshCw,
} from 'lucide-react';

export function PaymentDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const [payment, setPayment] = useState<Payment | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Refund state
  const [showRefundModal, setShowRefundModal] = useState(false);
  const [refundAmount, setRefundAmount] = useState('');
  const [refundReason, setRefundReason] = useState('');
  const [refundError, setRefundError] = useState<string | null>(null);
  const [refunding, setRefunding] = useState(false);

  // Action state
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  useEffect(() => {
    if (id) {
      loadPayment();
    }
  }, [id]);

  const loadPayment = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await apiClient.getPayment(id!);
      setPayment(data);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('Failed to load payment');
      }
    } finally {
      setLoading(false);
    }
  };

  const handleCapture = async () => {
    if (!payment) return;
    try {
      setActionLoading('capture');
      const updated = await apiClient.capturePayment(payment.id);
      setPayment(updated);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      }
    } finally {
      setActionLoading(null);
    }
  };

  const handleCancel = async () => {
    if (!payment) return;
    try {
      setActionLoading('cancel');
      const updated = await apiClient.cancelPayment(payment.id);
      setPayment(updated);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      }
    } finally {
      setActionLoading(null);
    }
  };

  const handleRefund = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!payment) return;

    setRefundError(null);
    setRefunding(true);

    try {
      await apiClient.refundPayment(payment.id, {
        amount: Math.round(parseFloat(refundAmount) * 100),
        currency: payment.currency,
        reason: refundReason,
      });

      // Reload payment to get updated refundable amount
      await loadPayment();
      setShowRefundModal(false);
      setRefundAmount('');
      setRefundReason('');
    } catch (err) {
      if (err instanceof ApiError) {
        setRefundError(err.message);
      } else {
        setRefundError('Failed to process refund');
      }
    } finally {
      setRefunding(false);
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'captured':
      case 'settled':
        return <CheckCircle className="h-8 w-8 text-green-500" />;
      case 'authorised':
        return <Clock className="h-8 w-8 text-yellow-500" />;
      case 'failed':
      case 'cancelled':
        return <XCircle className="h-8 w-8 text-red-500" />;
      default:
        return <Clock className="h-8 w-8 text-gray-500" />;
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

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <Loader2 className="h-8 w-8 animate-spin text-indigo-600" />
      </div>
    );
  }

  if (error || !payment) {
    return (
      <div>
        <Link
          to="/payments"
          className="inline-flex items-center text-sm text-indigo-600 hover:text-indigo-500 mb-4"
        >
          <ArrowLeft className="h-4 w-4 mr-1" />
          Back to Payments
        </Link>
        <div className="bg-red-50 p-4 rounded-md flex items-center">
          <AlertCircle className="h-5 w-5 text-red-400 mr-2" />
          <span className="text-sm text-red-700">{error || 'Payment not found'}</span>
        </div>
      </div>
    );
  }

  const canCapture = payment.status === 'authorised';
  const canCancel = payment.status === 'created' || payment.status === 'authorised';
  const canRefund = payment.status === 'settled';
  const refundableAmount = (payment.amount / 100).toFixed(2); // Simplified - should calculate actual refundable amount

  return (
    <div>
      <Link
        to="/payments"
        className="inline-flex items-center text-sm text-indigo-600 hover:text-indigo-500 mb-4"
      >
        <ArrowLeft className="h-4 w-4 mr-1" />
        Back to Payments
      </Link>

      {error && (
        <div className="mb-4 p-4 bg-red-50 rounded-md flex items-center">
          <AlertCircle className="h-5 w-5 text-red-400 mr-2" />
          <span className="text-sm text-red-700">{error}</span>
        </div>
      )}

      <div className="bg-white shadow overflow-hidden sm:rounded-lg">
        <div className="px-4 py-5 sm:px-6 flex items-center justify-between">
          <div className="flex items-center">
            {getStatusIcon(payment.status)}
            <div className="ml-4">
              <h3 className="text-lg leading-6 font-medium text-gray-900">
                Payment Details
              </h3>
              <p className="mt-1 text-sm text-gray-500">{payment.id}</p>
            </div>
          </div>
          <span
            className={`px-3 py-1 inline-flex text-sm leading-5 font-semibold rounded-full ${getStatusColor(
              payment.status
            )}`}
          >
            {payment.status}
          </span>
        </div>

        <div className="border-t border-gray-200 px-4 py-5 sm:px-6">
          <dl className="grid grid-cols-1 gap-x-4 gap-y-6 sm:grid-cols-2">
            <div>
              <dt className="text-sm font-medium text-gray-500">Amount</dt>
              <dd className="mt-1 text-sm text-gray-900">
                {(payment.amount / 100).toFixed(2)} {payment.currency}
              </dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-gray-500">Mode</dt>
              <dd className="mt-1 text-sm text-gray-900">
                <span
                  className={`px-2 py-1 rounded text-xs ${
                    payment.mode === 'live'
                      ? 'bg-green-100 text-green-800'
                      : 'bg-yellow-100 text-yellow-800'
                  }`}
                >
                  {payment.mode}
                </span>
              </dd>
            </div>
            <div>
              <dt className="text-sm font-medium text-gray-500">Created</dt>
              <dd className="mt-1 text-sm text-gray-900">
                {new Date(payment.createdAt).toLocaleString()}
              </dd>
            </div>
            {payment.gatewayReference && (
              <div>
                <dt className="text-sm font-medium text-gray-500">
                  Gateway Reference
                </dt>
                <dd className="mt-1 text-sm text-gray-900">
                  {payment.gatewayReference}
                </dd>
              </div>
            )}
            {payment.failureReason && (
              <div className="sm:col-span-2">
                <dt className="text-sm font-medium text-gray-500">
                  Failure Reason
                </dt>
                <dd className="mt-1 text-sm text-red-600">
                  {payment.failureReason}
                </dd>
              </div>
            )}
          </dl>
        </div>

        {/* Actions */}
        <div className="border-t border-gray-200 px-4 py-4 sm:px-6">
          <div className="flex space-x-3">
            {canCapture && (
              <button
                onClick={handleCapture}
                disabled={actionLoading !== null}
                className="inline-flex items-center px-4 py-2 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-green-600 hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-green-500 disabled:opacity-50"
              >
                {actionLoading === 'capture' ? (
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                ) : (
                  <CheckCircle className="h-4 w-4 mr-2" />
                )}
                Capture
              </button>
            )}
            {canCancel && (
              <button
                onClick={handleCancel}
                disabled={actionLoading !== null}
                className="inline-flex items-center px-4 py-2 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50"
              >
                {actionLoading === 'cancel' ? (
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                ) : (
                  <XCircle className="h-4 w-4 mr-2" />
                )}
                Cancel
              </button>
            )}
            {canRefund && (
              <button
                onClick={() => setShowRefundModal(true)}
                disabled={actionLoading !== null}
                className="inline-flex items-center px-4 py-2 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 disabled:opacity-50"
              >
                <RefreshCw className="h-4 w-4 mr-2" />
                Issue Refund
              </button>
            )}
          </div>
        </div>
      </div>

      {/* Refund Modal */}
      {showRefundModal && (
        <div className="fixed z-10 inset-0 overflow-y-auto">
          <div className="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
            <div
              className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity"
              onClick={() => {
                setShowRefundModal(false);
                setRefundAmount('');
                setRefundReason('');
                setRefundError(null);
              }}
            />

            <div className="inline-block align-bottom bg-white rounded-lg px-4 pt-5 pb-4 text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-lg sm:w-full sm:p-6">
              <div>
                <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-indigo-100">
                  <RefreshCw className="h-6 w-6 text-indigo-600" />
                </div>
                <div className="mt-3 text-center sm:mt-5">
                  <h3 className="text-lg leading-6 font-medium text-gray-900">
                    Issue Refund
                  </h3>
                  <div className="mt-2">
                    <p className="text-sm text-gray-500">
                      Refundable amount: {refundableAmount} {payment.currency}
                    </p>
                  </div>
                </div>
              </div>

              <form onSubmit={handleRefund} className="mt-4">
                {refundError && (
                  <div className="mb-4 p-3 bg-red-50 rounded-md flex items-center">
                    <AlertCircle className="h-4 w-4 text-red-400 mr-2" />
                    <span className="text-sm text-red-700">{refundError}</span>
                  </div>
                )}

                <div className="space-y-4">
                  <div>
                    <label
                      htmlFor="refundAmount"
                      className="block text-sm font-medium text-gray-700"
                    >
                      Refund Amount ({payment.currency})
                    </label>
                    <input
                      type="number"
                      id="refundAmount"
                      step="0.01"
                      min="0.01"
                      max={refundableAmount}
                      required
                      className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm"
                      value={refundAmount}
                      onChange={(e) => setRefundAmount(e.target.value)}
                      disabled={refunding}
                    />
                  </div>

                  <div>
                    <label
                      htmlFor="refundReason"
                      className="block text-sm font-medium text-gray-700"
                    >
                      Reason
                    </label>
                    <textarea
                      id="refundReason"
                      rows={3}
                      required
                      className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm"
                      value={refundReason}
                      onChange={(e) => setRefundReason(e.target.value)}
                      disabled={refunding}
                    />
                  </div>
                </div>

                <div className="mt-5 sm:mt-6 sm:grid sm:grid-cols-2 sm:gap-3 sm:grid-flow-row-dense">
                  <button
                    type="submit"
                    disabled={refunding}
                    className="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-indigo-600 text-base font-medium text-white hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:col-start-2 sm:text-sm disabled:opacity-50"
                  >
                    {refunding ? (
                      <>
                        <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                        Processing...
                      </>
                    ) : (
                      'Process Refund'
                    )}
                  </button>
                  <button
                    type="button"
                    className="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:mt-0 sm:col-start-1 sm:text-sm"
                    onClick={() => {
                      setShowRefundModal(false);
                      setRefundAmount('');
                      setRefundReason('');
                      setRefundError(null);
                    }}
                    disabled={refunding}
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