import { useState, useEffect } from 'react';
import { apiClient, ApiError } from '../api/client';
import type { SettlementBatch } from '../types';
import {
  DollarSign,
  Calendar,
  AlertCircle,
  Loader2,
  CheckCircle,
  Clock,
  XCircle,
} from 'lucide-react';

export function SettlementsPage() {
  const [settlements, setSettlements] = useState<SettlementBatch[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [selectedSettlement, setSelectedSettlement] = useState<SettlementBatch | null>(null);

  useEffect(() => {
    loadSettlements();
  }, []);

  const loadSettlements = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await apiClient.getSettlements(fromDate || undefined, toDate || undefined);
      setSettlements(data);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('Failed to load settlements');
      }
    } finally {
      setLoading(false);
    }
  };

  const handleFilter = () => {
    loadSettlements();
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'completed':
        return <CheckCircle className="h-5 w-5 text-green-500" />;
      case 'pending':
        return <Clock className="h-5 w-5 text-yellow-500" />;
      case 'failed':
        return <XCircle className="h-5 w-5 text-red-500" />;
      default:
        return <Clock className="h-5 w-5 text-gray-500" />;
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'completed':
        return 'bg-green-100 text-green-800';
      case 'pending':
        return 'bg-yellow-100 text-yellow-800';
      case 'failed':
        return 'bg-red-100 text-red-800' ;
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

  return (
    <div>
      <div className="sm:flex sm:items-center sm:justify-between mb-8">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900">Settlements</h1>
          <p className="mt-1 text-sm text-gray-500">
            View settlement batches and reconciliation data.
          </p>
        </div>
      </div>

      {/* Date Filters */}
      <div className="mb-6 bg-white p-4 rounded-lg shadow">
        <div className="flex flex-wrap items-end gap-4">
          <div>
            <label
              htmlFor="fromDate"
              className="block text-sm font-medium text-gray-700"
            >
              From Date
            </label>
            <input
              type="date"
              id="fromDate"
              className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
            />
          </div>
          <div>
            <label
              htmlFor="toDate"
              className="block text-sm font-medium text-gray-700"
            >
              To Date
            </label>
            <input
              type="date"
              id="toDate"
              className="mt-1 block w-full border border-gray-300 rounded-md shadow-sm py-2 px-3 focus:outline-none focus:ring-indigo-500 focus:border-indigo-500 sm:text-sm"
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
            />
          </div>
          <button
            onClick={handleFilter}
            className="inline-flex items-center px-4 py-2 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500"
          >
            <Calendar className="h-4 w-4 mr-2" />
            Filter
          </button>
        </div>
      </div>

      {error && (
        <div className="mb-4 p-4 bg-red-50 rounded-md flex items-center">
          <AlertCircle className="h-5 w-5 text-red-400 mr-2" />
          <span className="text-sm text-red-700">{error}</span>
        </div>
      )}

      {/* Settlements Table */}
      <div className="bg-white shadow overflow-hidden sm:rounded-lg">
        <table className="min-w-full divide-y divide-gray-200">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                Settlement Date
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                Gross Amount
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                Fees
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                Net Amount
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                Payments
              </th>
              <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                Status
              </th>
            </tr>
          </thead>
          <tbody className="bg-white divide-y divide-gray-200">
            {settlements.length === 0 ? (
              <tr>
                <td
                  colSpan={6}
                  className="px-6 py-8 text-center text-gray-500"
                >
                  <DollarSign className="h-12 w-12 mx-auto text-gray-300 mb-4" />
                  <p>No settlements found.</p>
                  <p className="text-sm">
                    Settlement batches are created nightly at 00:30 UTC.
                  </p>
                </td>
              </tr>
            ) : (
              settlements.map((settlement) => (
                <tr
                  key={settlement.id}
                  className="hover:bg-gray-50 cursor-pointer"
                  onClick={() => setSelectedSettlement(settlement)}
                >
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {settlement.settlementDate}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                    {settlement.grossAmount.toFixed(2)} {settlement.currency}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                    -{settlement.feeAmount.toFixed(2)} {settlement.currency}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900">
                    {settlement.netAmount.toFixed(2)} {settlement.currency}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                    {settlement.paymentCount}
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <span
                      className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${getStatusColor(
                        settlement.status
                      )}`}
                    >
                      {getStatusIcon(settlement.status)}
                      <span className="ml-1">{settlement.status}</span>
                    </span>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Settlement Details Modal */}
      {selectedSettlement && (
        <div className="fixed z-10 inset-0 overflow-y-auto">
          <div className="flex items-end justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
            <div
              className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity"
              onClick={() => setSelectedSettlement(null)}
            />

            <div className="inline-block align-bottom bg-white rounded-lg px-4 pt-5 pb-4 text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-lg sm:w-full sm:p-6">
              <div>
                <div className="mx-auto flex items-center justify-center h-12 w-12 rounded-full bg-indigo-100">
                  <DollarSign className="h-6 w-6 text-indigo-600" />
                </div>
                <div className="mt-3 text-center sm:mt-5">
                  <h3 className="text-lg leading-6 font-medium text-gray-900">
                    Settlement Details
                  </h3>
                  <p className="mt-1 text-sm text-gray-500">
                    {selectedSettlement.id}
                  </p>
                </div>
              </div>

              <div className="mt-4 border-t border-gray-200 pt-4">
                <dl className="space-y-3">
                  <div className="flex justify-between">
                    <dt className="text-sm text-gray-500">Settlement Date</dt>
                    <dd className="text-sm font-medium text-gray-900">
                      {selectedSettlement.settlementDate}
                    </dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-sm text-gray-500">Gross Amount</dt>
                    <dd className="text-sm font-medium text-gray-900">
                      {selectedSettlement.grossAmount.toFixed(2)}{' '}
                      {selectedSettlement.currency}
                    </dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-sm text-gray-500">Fees</dt>
                    <dd className="text-sm font-medium text-red-600">
                      -{selectedSettlement.feeAmount.toFixed(2)}{' '}
                      {selectedSettlement.currency}
                    </dd>
                  </div>
                  <div className="flex justify-between border-t border-gray-200 pt-3">
                    <dt className="text-sm font-medium text-gray-900">
                      Net Amount
                    </dt>
                    <dd className="text-sm font-bold text-gray-900">
                      {selectedSettlement.netAmount.toFixed(2)}{' '}
                      {selectedSettlement.currency}
                    </dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-sm text-gray-500">Payment Count</dt>
                    <dd className="text-sm font-medium text-gray-900">
                      {selectedSettlement.paymentCount}
                    </dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-sm text-gray-500">Status</dt>
                    <dd>
                      <span
                        className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${getStatusColor(
                          selectedSettlement.status
                        )}`}
                      >
                        {selectedSettlement.status}
                      </span>
                    </dd>
                  </div>
                  <div className="flex justify-between">
                    <dt className="text-sm text-gray-500">Created</dt>
                    <dd className="text-sm text-gray-900">
                      {new Date(selectedSettlement.createdAt).toLocaleString()}
                    </dd>
                  </div>
                  {selectedSettlement.completedAt && (
                    <div className="flex justify-between">
                      <dt className="text-sm text-gray-500">Completed</dt>
                      <dd className="text-sm text-gray-900">
                        {new Date(selectedSettlement.completedAt).toLocaleString()}
                      </dd>
                    </div>
                  )}
                </dl>
              </div>

              <div className="mt-5 sm:mt-6">
                <button
                  type="button"
                  className="w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:text-sm"
                  onClick={() => setSelectedSettlement(null)}
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}