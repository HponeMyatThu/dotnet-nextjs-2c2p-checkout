'use client';

import { useEffect, useState, useCallback, useRef, Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import { Transaction } from '@/types/api';
import { fetchTransaction } from '@/services/api';
import StatusBadge from '@/components/StatusBadge';
import Spinner from '@/components/Spinner';
import Link from 'next/link';

const POLL_INTERVAL_MS = 3000;

function ResultContent() {
  const searchParams = useSearchParams();
  // Support ?invoice=, ?invoiceNo=, or ?orderNo= (2C2P redirect params)
  const invoiceNo =
    searchParams.get('invoice') ??
    searchParams.get('invoiceNo') ??
    searchParams.get('orderNo');

  const [transaction, setTransaction] = useState<Transaction | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const isMounted = useRef(true);

  const stopPolling = useCallback(() => {
    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  }, []);

  const fetchStatus = useCallback(async () => {
    if (!invoiceNo) return;
    try {
      const tx = await fetchTransaction(invoiceNo);
      if (!isMounted.current) return;
      setTransaction(tx);
      setLoading(false);
      // Stop polling when we reach a final state
      if (tx.status === 'Succeeded' || tx.status === 'Failed') {
        stopPolling();
      }
    } catch (err: unknown) {
      if (!isMounted.current) return;
      const msg = err instanceof Error ? err.message : 'Failed to fetch payment status';
      setError(msg);
      setLoading(false);
      stopPolling();
    }
  }, [invoiceNo, stopPolling]);

  useEffect(() => {
    isMounted.current = true;
    if (!invoiceNo) {
      setError('No invoice number provided.');
      setLoading(false);
      return;
    }

    // Fetch immediately then poll
    fetchStatus();
    intervalRef.current = setInterval(fetchStatus, POLL_INTERVAL_MS);

    return () => {
      isMounted.current = false;
      stopPolling();
    };
  }, [fetchStatus, invoiceNo, stopPolling]);

  if (!invoiceNo) {
    return (
      <ResultContainer>
        <div className="text-center">
          <div className="text-5xl mb-4">❌</div>
          <h2 className="text-xl font-bold text-red-600 mb-2">Invalid Result Page</h2>
          <p className="text-gray-500 text-sm mb-6">No invoice number found in URL.</p>
          <Link
            href="/checkout"
            className="inline-block rounded-lg bg-indigo-600 text-white px-6 py-2 text-sm font-semibold hover:bg-indigo-700"
          >
            Back to Checkout
          </Link>
        </div>
      </ResultContainer>
    );
  }

  if (loading) {
    return (
      <ResultContainer>
        <Spinner size="lg" label="Fetching payment status..." />
      </ResultContainer>
    );
  }

  if (error || !transaction) {
    return (
      <ResultContainer>
        <div className="text-center">
          <div className="text-5xl mb-4">⚠️</div>
          <h2 className="text-xl font-bold text-red-600 mb-2">Error</h2>
          <p className="text-gray-500 text-sm mb-6">{error ?? 'Transaction not found'}</p>
          <Link
            href="/checkout"
            className="inline-block rounded-lg bg-indigo-600 text-white px-6 py-2 text-sm font-semibold hover:bg-indigo-700"
          >
            Back to Checkout
          </Link>
        </div>
      </ResultContainer>
    );
  }

  const retryHref = `/checkout?customerId=${transaction.customerId}&productId=${transaction.productId}`;

  return (
    <div className="min-h-screen bg-gradient-to-br from-indigo-50 to-white py-12 px-4">
      <div className="mx-auto max-w-lg">
        {/* Status card */}
        <div className="rounded-2xl bg-white shadow-sm border border-gray-200 overflow-hidden">
          {/* Top banner */}
          <div className={`px-6 py-8 text-center ${
            transaction.status === 'Succeeded' ? 'bg-green-50' :
            transaction.status === 'Failed' ? 'bg-red-50' :
            'bg-yellow-50'
          }`}>
            <div className="text-6xl mb-4">
              {transaction.status === 'Succeeded' ? '✅' : transaction.status === 'Failed' ? '❌' : '⏳'}
            </div>
            <h1 className="text-2xl font-bold text-gray-900 mb-2">
              {transaction.status === 'Succeeded' ? 'Payment Succeeded' :
               transaction.status === 'Failed' ? 'Payment Failed' :
               'Processing Payment...'}
            </h1>
            <div className="flex justify-center mt-2">
              <StatusBadge status={transaction.status} />
            </div>
            {transaction.status === 'Pending' && (
              <div className="mt-4 flex justify-center">
                <Spinner size="sm" label="Waiting for confirmation..." />
              </div>
            )}
          </div>

          {/* Details */}
          <div className="px-6 py-6 space-y-4">
            <dl className="space-y-3 text-sm">
              <InfoRow label="Invoice No" value={transaction.invoiceNo} mono />
              {transaction.productName && (
                <InfoRow label="Product" value={transaction.productName} />
              )}
              <InfoRow label="Amount" value={`${transaction.currency} ${Number(transaction.amount).toFixed(2)}`} />
              {transaction.paymentChannel && (
                <InfoRow label="Payment Method" value={transaction.paymentChannel} />
              )}
              {transaction.maskedCardNo && (
                <InfoRow label="Card / Account" value={transaction.maskedCardNo} mono />
              )}
              {transaction.providerRef && (
                <InfoRow label="Transaction Ref" value={transaction.providerRef} mono />
              )}
              {transaction.respCode && (
                <InfoRow label="Response Code" value={transaction.respCode} mono />
              )}
              {transaction.respDesc && (
                <InfoRow label={transaction.status === 'Failed' ? 'Failure Reason' : 'Response'} value={transaction.respDesc} />
              )}
              {transaction.paidAt && (
                <InfoRow label="Paid At" value={new Date(transaction.paidAt).toLocaleString()} />
              )}
              <InfoRow label="Created At" value={new Date(transaction.createdAt).toLocaleString()} />
            </dl>
          </div>

          {/* Actions */}
          <div className="px-6 pb-6 flex gap-3">
            {transaction.status === 'Failed' && (
              <Link
                href={retryHref}
                className="flex-1 rounded-lg bg-indigo-600 py-2.5 text-center text-sm font-semibold text-white hover:bg-indigo-700 transition-colors"
              >
                Go Back &amp; Try Again
              </Link>
            )}
            {transaction.status === 'Succeeded' && (
              <Link
                href="/checkout"
                className="flex-1 rounded-lg bg-indigo-600 py-2.5 text-center text-sm font-semibold text-white hover:bg-indigo-700 transition-colors"
              >
                New Payment
              </Link>
            )}
          </div>
        </div>

        {/* Note: status is fetched from backend only */}
        <p className="text-center text-xs text-gray-400 mt-4">
          Payment status is verified server-side — not from URL parameters.
        </p>
      </div>
    </div>
  );
}

function ResultContainer({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-indigo-50 to-white px-4">
      <div className="rounded-2xl bg-white shadow-sm border border-gray-200 p-10 w-full max-w-sm">
        {children}
      </div>
    </div>
  );
}

function InfoRow({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex justify-between gap-4">
      <dt className="text-gray-500 shrink-0">{label}</dt>
      <dd className={`text-right text-gray-900 font-medium ${mono ? 'font-mono text-xs' : ''}`}>{value}</dd>
    </div>
  );
}

export default function ResultPage() {
  return (
    <Suspense
      fallback={
        <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-indigo-50 to-white px-4">
          <Spinner size="lg" label="Loading result..." />
        </div>
      }
    >
      <ResultContent />
    </Suspense>
  );
}
